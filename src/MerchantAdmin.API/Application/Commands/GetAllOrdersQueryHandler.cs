using MerchantAdmin.API.Application.Dtos;

namespace MerchantAdmin.API.Application.Commands
{
    public class GetAllOrdersQueryHandler(AppDbContext db) : IRequestHandler<GetAllOrdersQuery, PagedResult<OrderDto>>
    {
        public async Task<PagedResult<OrderDto>> Handle(GetAllOrdersQuery request, CancellationToken ct)
        {
            var query = db.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .Where(o => !o.IsDeleted)
                .AsQueryable();

            // 按订单号精确搜索
            if (request.OrderId.HasValue)
            {
                query = query.Where(o => o.Id == request.OrderId.Value);
            }

            // 按订单状态搜索
            if (!string.IsNullOrWhiteSpace(request.Status)
                && Enum.TryParse<OrderStatus>(request.Status, true, out var status))
            {
                query = query.Where(o => o.OrderStatus == status);
            }

            var total = await query.CountAsync(ct);

            var orders = await query
                .OrderByDescending(o => o.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            // 商品名使用订单明细中的快照（商品后续改名不影响历史订单）
            var items = orders.Select(o => new OrderDto(
                 o.Id,
                 o.CreatedAt,
                 o.OrderStatus,
                 o.OrderItems.Select(oi => new OrderItemDto(
                     oi.ProductId,
                     oi.ProductName,
                     oi.Price,
                     oi.Quantity
                 )).ToList()
             )).ToList();

            return new PagedResult<OrderDto>(total, request.Page, request.PageSize, items);
        }
    }
}
