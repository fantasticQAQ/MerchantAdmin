using MediatR;
using MerchantAdmin.Application.Dtos;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MerchantAdmin.Application.Commands
{
    public class GetAllProductsQueryHandler(AppDbContext db, ICacheService cache) : IRequestHandler<GetAllProductsQuery, List<ProductDto>>
    {
        public async Task<List<ProductDto>> Handle(GetAllProductsQuery request, CancellationToken ct)
        {
            const string cacheKey = "products:all";

            var dtos = await cache.GetAsync<List<ProductDto>>(cacheKey, ct);
            if (dtos != null)
            {
                return dtos;
            }

            var products = await db.Products
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .ToListAsync(ct);

            dtos = products.Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock)).ToList();

            await cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(10), ct);

            return dtos;
        }
    }
}
