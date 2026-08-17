using MediatR;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;

namespace MerchantAdmin.Application.Commands
{
    public class CreateOrderCommandHandler(AppDbContext db, IDelayJobService delayJob, ICacheService cache)
            : IRequestHandler<CreateOrderCommand, int>
    {
        public async Task<int> Handle(CreateOrderCommand cmd, CancellationToken ct)
        {
            var productIds = cmd.OrderItems.Select(x => x.ProductId).Distinct().ToList();

            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var order = new Order();
            foreach (var item in cmd.OrderItems)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                    throw new DomainException("商品不存在");

                // 下架商品不可下单
                if (!product.IsActive)
                    throw new DomainException($"商品「{product.Name}」已下架，无法购买");

                order.AddOrderItem(product, item.Quantity);
            }

            db.Orders.Add(order);

            // 保存
            await db.SaveEntitiesAsync(ct);

            // Redis 延迟取消
            await delayJob.ScheduleCancelOrderAsync(order.Id, TimeSpan.FromMinutes(15));

            // 失效商品缓存（库存已扣减，避免前端读到过期库存）
            await cache.RemoveAsync("products:all", ct);

            return order.Id;
        }
    }
}
