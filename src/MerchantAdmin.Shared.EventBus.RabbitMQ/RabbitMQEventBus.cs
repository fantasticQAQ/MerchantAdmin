using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using MerchantAdmin.Shared.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MerchantAdmin.Shared.EventBus.RabbitMQ
{
    public sealed class RabbitMQEventBus(
    ILogger<RabbitMQEventBus> logger,
     IOptions<EventBusSubscriptionInfo> subscriptionOptions,
    IOptions<EventBusOptions> options,
    IServiceProvider _serviceProvider) : IEventBus, IHostedService, IDisposable
    {
        private const string ExchangeName = "merchantAdmin_events";

        private readonly EventBusSubscriptionInfo _subscriptionInfo = subscriptionOptions.Value;
        // 队列名按服务配置（SubscriptionClientName），默认保持原队列名以兼容
        private readonly string _queueName = options.Value.SubscriptionClientName ?? "merchantAdmin_queue";
        private IConnection rabbitMQConnection;
        private IChannel _consumerChannel;

        public async Task PublishAsync(IntegrationEvent @event)
        {
            // 创建 Channel（轻量级）；publisherConfirmationsEnabled=true 启用 Publisher Confirm
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            await using var channel = await rabbitMQConnection.CreateChannelAsync(channelOptions);

            // 声明 Exchange（durable：交换机元数据落盘）
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true);

            // RoutingKey = 事件名
            var routingKey = @event.GetType().Name;

            // 序列化
            var body = SerializeMessage(@event);

            // 消息属性（DeliveryMode=PERSISTENT：消息内容落盘，配合持久化队列保证重启不丢）
            var props = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent
            };

            // 记录本次发布序号，用于匹配 Broker 的 ack/nack
            var seqNo = await channel.GetNextPublishSequenceNumberAsync();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task OnAck(object? s, BasicAckEventArgs e)
            {
                if (e.DeliveryTag >= seqNo)
                {
                    tcs.TrySetResult(true);
                }
                await Task.CompletedTask;
            }
            async Task OnNack(object? s, BasicNackEventArgs e)
            {
                if (e.DeliveryTag >= seqNo)
                {
                    tcs.TrySetResult(false);
                }
                await Task.CompletedTask;
            }
            channel.BasicAcksAsync += OnAck;
            channel.BasicNacksAsync += OnNack;

            try
            {
                // 发布
                await channel.BasicPublishAsync(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: props,
                    body: body);

                // 等待 Broker 确认：nack/超时视为投递失败（可在此扩展重试/落库补偿）
                var confirmed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (!confirmed)
                {
                    logger.LogWarning("Publisher confirm failed (nack) for event {EventName}", routingKey);
                }
            }
            catch (TimeoutException)
            {
                logger.LogWarning("Publisher confirm timeout for event {EventName}", routingKey);
            }
            finally
            {
                channel.BasicAcksAsync -= OnAck;
                channel.BasicNacksAsync -= OnNack;
                await channel.CloseAsync();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Factory.StartNew((Func<Task>)(async () =>
            {
                try
                {
                    rabbitMQConnection = _serviceProvider.GetRequiredService<IConnection>();
                    if (!rabbitMQConnection.IsOpen)
                    {
                        return;
                    }

                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace("Creating RabbitMQ consumer channel");
                    }

                    _consumerChannel = await rabbitMQConnection.CreateChannelAsync();

                    _consumerChannel.CallbackExceptionAsync += (sender, ea) =>
                    {
                        logger.LogWarning(ea.Exception, "Error with RabbitMQ consumer channel");
                        return Task.CompletedTask;
                    };

                    await _consumerChannel.ExchangeDeclareAsync(
                        exchange: ExchangeName,
                        durable: true,
                        type: ExchangeType.Topic);

                    // 主队列：durable 持久化 + 绑定死信（处理失败的消息进 DLQ，避免无限重投）
                    var queueArgs = new Dictionary<string, object>
                    {
                        { "x-dead-letter-exchange", "" },                    // 默认交换机
                        { "x-dead-letter-routing-key", $"{_queueName}.dlq" } // 死信路由到 DLQ
                    };
                    await _consumerChannel.QueueDeclareAsync(
                        queue: _queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: queueArgs);

                    // 死信队列（DLQ）：接收处理失败的消息，供人工/定时重放
                    await _consumerChannel.QueueDeclareAsync(
                        queue: $"{_queueName}.dlq",
                        durable: true,
                        exclusive: false,
                        autoDelete: false);

                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace("Starting RabbitMQ basic consume");
                    }

                    // 绑定你想监听的事件
                    await _consumerChannel.QueueBindAsync(
                        queue: _queueName,
                        exchange: ExchangeName,
                        routingKey: "#");

                    var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

                    consumer.ReceivedAsync += async (_, args) =>
                    {
                        var eventName = args.RoutingKey;
                        var json = Encoding.UTF8.GetString(args.Body.Span);
                        if (!_subscriptionInfo.EventTypes.TryGetValue(eventName, out var eventType))
                        {
                            // 未订阅的事件类型：跳过并确认，避免消息积压和重复投递
                            logger.LogWarning("Unable to resolve event type for event name {EventName}", eventName);
                            await _consumerChannel.BasicAckAsync(
                                deliveryTag: args.DeliveryTag,
                                multiple: false);
                            return;
                        }

                        try
                        {
                            var integrationEvent = DeserializeMessage(json, eventType);

                            await using var scope = _serviceProvider.CreateAsyncScope();
                            foreach (var handler in scope.ServiceProvider.GetKeyedServices<IIntegrationEventHandler>(eventType))
                            {
                                await handler.Handle(integrationEvent);
                            }

                            // 处理成功：手动确认（At Least Once——消费者崩溃后由 RabbitMQ 重新投递）
                            await _consumerChannel.BasicAckAsync(
                                deliveryTag: args.DeliveryTag,
                                multiple: false);
                        }
                        catch (Exception ex)
                        {
                            // 处理失败：Nack + requeue=false → 消息进入死信队列（DLQ），避免无限重投死循环
                            logger.LogError(ex, "Error handling event {EventName}", eventName);
                            await _consumerChannel.BasicNackAsync(
                                deliveryTag: args.DeliveryTag,
                                multiple: false,
                                requeue: false);
                        }
                    };

                    await _consumerChannel.BasicConsumeAsync(
                        queue: _queueName,
                        autoAck: false,
                        consumer: consumer);

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error starting RabbitMQ connection");
                    return;
                }
            }),
            TaskCreationOptions.LongRunning);

            return Task.CompletedTask;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
         Justification = "The 'JsonSerializer.IsReflectionEnabledByDefault' feature switch, which is set to false by default for trimmed .NET apps, ensures the JsonSerializer doesn't use Reflection.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "See above.")]
        private IntegrationEvent DeserializeMessage(string message, Type eventType)
        {
            return JsonSerializer.Deserialize(message, eventType, _subscriptionInfo.JsonSerializerOptions) as IntegrationEvent;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "The 'JsonSerializer.IsReflectionEnabledByDefault' feature switch, which is set to false by default for trimmed .NET apps, ensures the JsonSerializer doesn't use Reflection.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "See above.")]
        private byte[] SerializeMessage(IntegrationEvent @event)
        {
            return JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _subscriptionInfo.JsonSerializerOptions);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _consumerChannel?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _consumerChannel?.Dispose();
        }
    }
}
