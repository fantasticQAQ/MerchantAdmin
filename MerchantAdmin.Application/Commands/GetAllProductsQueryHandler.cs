using MediatR;
using MerchantAdmin.Application.Dtos;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MerchantAdmin.Application.Commands
{
    public class GetAllProductsQueryHandler(AppDbContext db) : IRequestHandler<GetAllProductsQuery, PagedResult<ProductDto>>
    {
        public async Task<PagedResult<ProductDto>> Handle(GetAllProductsQuery request, CancellationToken ct)
        {
            var query = db.Products
                .AsNoTracking()
                .AsQueryable();

            // 按名称搜索
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                query = query.Where(p => p.Name.Contains(request.Name));
            }

            var total = await query.CountAsync(ct);

            var products = await query
                .OrderBy(p => p.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            var items = products
                .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock, p.IsActive))
                .ToList();

            return new PagedResult<ProductDto>(total, request.Page, request.PageSize, items);
        }
    }
}
