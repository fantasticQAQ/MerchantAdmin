using MediatR;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;

namespace MerchantAdmin.Application.Commands
{
    public class RefundOrderCommandHandler(AppDbContext db, ICacheService cache) : IRequestHandler<RefundOrderCommand, bool>
    {
        public async Task<bool> Handle(RefundOrderCommand request, CancellationToken ct)
        {
            var order = await db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);

            if (order is null)
            {
                return false;
            }

            // 领域状态机：仅已支付订单可退款（Paid → Refunded）
            order.MarkAsRefunded();

            // 退款即交易取消：商品退回，回补库存。
            // 注意：此后删除该 Refunded 订单时不会再次回补（DeleteOrderCommandHandler 仅对
            // Created/PaymentProcessing/Paid 回补），避免重复回补。
            var productIds = order.OrderItems.Select(x => x.ProductId).ToList();
            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var item in order.OrderItems)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    product.IncreaseStock(item.Quantity);
                }
            }

            await db.SaveEntitiesAsync(ct);

            // 库存已变化，失效商品缓存
            await cache.RemoveAsync("products:all", ct);

            return true;
        }
    }
}
