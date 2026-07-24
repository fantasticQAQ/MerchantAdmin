using MediatR;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;

public class CreateOrderCommandHandler(AppDbContext db, IDelayJobService delayJob)
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

            order.AddOrderItem(product, item.Quantity);
        }

        db.Orders.Add(order);

        // 3、保存
        await db.SaveEntitiesAsync(ct);

        // Redis 延迟取消
        await delayJob.ScheduleCancelOrderAsync(order.Id, TimeSpan.FromMinutes(15));

        return order.Id;
    }
}
