using MediatR;
using MerchantAdmin.Domain.Events;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MerchantAdmin.Application.DomainEventHandlers
{
    public class OrderCancelledDomainEventHandler(AppDbContext db) : INotificationHandler<OrderCancelledDomainEvent>
    {
        public async Task Handle(OrderCancelledDomainEvent evt, CancellationToken ct)
        {
            var productIds = evt.Order.OrderItems
                .Select(x => x.ProductId)
                .ToList();

            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var item in evt.Order.OrderItems)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                    throw new DomainException($"商品不存在：{item.ProductId}");

                product.IncreaseStock(item.Quantity);
            }
        }
    }
}
