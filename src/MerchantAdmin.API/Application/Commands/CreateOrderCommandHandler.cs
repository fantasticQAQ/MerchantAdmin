using MerchantAdmin.API.Infrastructure.Caching;
using MerchantAdmin.API.Application.Services;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Exceptions;

namespace MerchantAdmin.API.Application.Commands
{
    public class CreateOrderCommandHandler(
        AppDbContext db,
        IDelayJobService delayJob,
        IProductStockService stock,
        IProductListCacheInvalidator productListCache)
            : IRequestHandler<CreateOrderCommand, int>
    {
        public async Task<int> Handle(CreateOrderCommand cmd, CancellationToken ct)
        {
            var productIds = cmd.OrderItems.Select(x => x.ProductId).Distinct().ToList();

            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var item in cmd.OrderItems)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                    throw new DomainException("商品不存在");

                if (!product.IsActive)
                    throw new DomainException($"商品「{product.Name}」已下架，无法购买");
            }

            foreach (var group in cmd.OrderItems.GroupBy(x => x.ProductId))
            {
                await stock.DecreaseAsync(group.Key, group.Sum(x => x.Quantity), ct);
            }

            var order = new Order();
            foreach (var item in cmd.OrderItems)
            {
                order.AddOrderItem(products[item.ProductId], item.Quantity, deductStock: false);
            }

            db.Orders.Add(order);

            await db.SaveEntitiesAsync(ct);

            await delayJob.ScheduleCancelOrderAsync(order.Id, TimeSpan.FromMinutes(15));

            await productListCache.InvalidateAsync(ct);

            return order.Id;
        }
    }
}
