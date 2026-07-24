using MediatR;
using MerchantAdmin.Application.Dtos;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;

namespace MerchantAdmin.Application.Commands
{
    public class GetAllOrdersQueryHandler(AppDbContext db) : IRequestHandler<GetAllOrdersQuery, List<OrderDto>>
    {
        public async Task<List<OrderDto>> Handle(GetAllOrdersQuery request, CancellationToken ct)
        {
            var orders = await db.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .ToListAsync(ct);

            var productNames = await db.Products
                .AsNoTracking()
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            var dtos = orders.Select(o => new OrderDto(
                 o.Id,
                 o.CreatedAt,
                 o.OrderStatus,
                 o.OrderItems.Select(oi => new OrderItemDto(
                     oi.ProductId,
                     productNames[oi.ProductId], // 直接拿名字
                     oi.Price,
                     oi.Quantity
                 )).ToList()
             )).ToList();

            return dtos;
        }
    }
}
