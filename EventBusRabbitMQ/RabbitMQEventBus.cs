using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventBusRabbitMQ
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
            // 创建 Channel（轻量级）
            await using var channel = await rabbitMQConnection.CreateChannelAsync();

            // 声明 Exchange
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true);

            // RoutingKey = 事件名
            var routingKey = @event.GetType().Name;

            // 序列化
            var body = SerializeMessage(@event);

            // 消息属性
            var props = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent
            };

            // 发布
            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: props,
                body: body);

            await channel.CloseAsync();
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

                    await _consumerChannel.QueueDeclareAsync(
                        queue: _queueName,
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
                        var integrationEvent = DeserializeMessage(json, eventType);

                        await using var scope = _serviceProvider.CreateAsyncScope();
                        foreach (var handler in scope.ServiceProvider.GetKeyedServices<IIntegrationEventHandler>(eventType))
                        {
                            await handler.Handle(integrationEvent);
                        }

                        await _consumerChannel.BasicAckAsync(
                            deliveryTag: args.DeliveryTag,
                            multiple: false);
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
