using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System.Threading.Channels;

namespace MerchantAdmin.Application.Services;

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
        // 1️⃣ Redis 订阅（只负责转发）
        var sub = _provider.Connection.GetSubscriber();
        await sub.SubscribeAsync("__keyevent@0__:expired", (_, key) =>
        {
            if (!TryParseOrderId(key!, out var orderId))
                return;

            // 非阻塞写入 Channel
            _channel.Writer.TryWrite(orderId);
        });

        // 2️⃣ 异步消费
        await foreach (var orderId in _channel.Reader.ReadAllAsync(ct))
        {
            await ProcessCancelAsync(orderId, ct);
        }
    }

    private async Task ProcessCancelAsync(long orderId, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await db.Orders.FindAsync(orderId, ct);
        if (order == null || order.OrderStatus != OrderStatus.Created)
            return;

        order.Cancel();
        await db.SaveEntitiesAsync(ct);
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