using MerchantAdmin.API.Infrastructure.Caching;
using MerchantAdmin.Domain.Events;

namespace MerchantAdmin.API.Application.DomainEventHandlers
{
    /// <summary>
    /// 取消/超时关闭订单时回补库存（两种终态共用同一回补逻辑：
    /// 取消=交易未完成商品退回；超时关闭=未支付商品退回）。
    /// </summary>
    public class OrderCancelledDomainEventHandler(AppDbContext db, ICacheService cache, ILogger<OrderCancelledDomainEventHandler> logger)
        : INotificationHandler<OrderCancelledDomainEvent>, INotificationHandler<OrderTimedOutDomainEvent>
    {
        public Task Handle(OrderCancelledDomainEvent evt, CancellationToken ct)
            => RestoreStock(evt.Order, ct);

        public Task Handle(OrderTimedOutDomainEvent evt, CancellationToken ct)
            => RestoreStock(evt.Order, ct);

        private async Task RestoreStock(Order order, CancellationToken ct)
        {
            var productIds = order.OrderItems
                .Select(x => x.ProductId)
                .ToList();

            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var item in order.OrderItems)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                {
                    // 商品已被物理删除（历史数据异常）：库存无从回补，记录 Error 级日志
                    // （含完整上下文）供对账排查，但不阻断取消/退款流程。
                    // 正常路径下商品删除走"下架"（IsActive=false），记录仍在，不会走到这里。
                    logger.LogError("库存回补失败：订单 {OrderId} 的商品 {ProductId} 已不存在（可能被物理删除），数量 {Quantity}，需人工对账",
                        order.Id, item.ProductId, item.Quantity);
                    continue;
                }

                product.IncreaseStock(item.Quantity);
            }

            // 失效商品缓存（库存已变化）
            await cache.RemoveAsync("products:all", ct);
        }
    }
}
