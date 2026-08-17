using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System.Threading.Channels;

namespace MerchantAdmin.Application.Services;

/// <summary>
/// 监听 Redis key 过期事件，触发订单超时关闭（实时通道）。
/// 与 OrderTimeoutCompensationService（定时兜底扫描）构成双保险：
/// 过期事件负责实时触发，兜底扫描负责弥补事件丢失（Redis 通知不可靠/服务重启期间）。
/// </summary>
public sealed class RedisExpiredOrderConsumer : BackgroundService
{
    private readonly IRedisConnectionProvider _provider;
    private readonly IServiceProvider _sp;
    private readonly Channel<long> _channel;

    public RedisExpiredOrderConsumer(
        IRedisConnectionProvider provider,
        IServiceProvider sp)
    {
        _provider = provider;
        _sp = sp;
        _channel = Channel.CreateUnbounded<long>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Redis 订阅（只负责转发）
        var sub = _provider.Connection.GetSubscriber();
        await sub.SubscribeAsync("__keyevent@0__:expired", (_, key) =>
        {
            if (!TryParseOrderId(key!, out var orderId))
                return;

            // 非阻塞写入 Channel
            _channel.Writer.TryWrite(orderId);
        });

        // 异步消费
        await foreach (var orderId in _channel.Reader.ReadAllAsync(ct))
        {
            using var scope = _sp.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OrderTimeoutProcessor>();
            await processor.ProcessOrderAsync(orderId, ct);
        }
    }

    private static bool TryParseOrderId(RedisValue key, out int orderId)
    {
        orderId = 0;

        if (!key.HasValue)
            return false;

        const string prefix = "order:cancel:";
        var str = key.ToString();

        if (!str.StartsWith(prefix))
            return false;

        return int.TryParse(str[prefix.Length..], out orderId);
    }
}
