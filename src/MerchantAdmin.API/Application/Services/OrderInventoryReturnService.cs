using MerchantAdmin.API.Infrastructure.Caching;
using MerchantAdmin.Domain.Entities.AggregatesModel;

namespace MerchantAdmin.API.Application.Services;

/// <summary>把预占库存退回货架（幂等：同一订单只回补一次）。</summary>
public sealed class OrderInventoryReturnService(
    AppDbContext db,
    IProductStockService stock,
    IProductListCacheInvalidator productListCache,
    ILogger<OrderInventoryReturnService> logger)
{
    public async Task ReturnAsync(Order order, CancellationToken ct)
    {
        if (!order.TryBeginInventoryReturn())
            return;

        foreach (var item in order.OrderItems)
        {
            var exists = await db.Products.AnyAsync(p => p.Id == item.ProductId, ct);
            if (!exists)
            {
                logger.LogError("库存回补失败：订单 {OrderId} 的商品 {ProductId} 已不存在（可能被物理删除），数量 {Quantity}，需人工对账",
                    order.Id, item.ProductId, item.Quantity);
                continue;
            }

            await stock.IncreaseAsync(item.ProductId, item.Quantity, ct);
        }

        await productListCache.InvalidateAsync(ct);
    }
}
