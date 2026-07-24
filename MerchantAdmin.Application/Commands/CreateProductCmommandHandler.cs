using MediatR;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.Extensions.Logging;

namespace MerchantAdmin.Application.Commands
{
    public class CreateProductCmommandHandler(AppDbContext db, ICacheService cache) : IRequestHandler<CreateProductCmommand, int>
    {
        public async Task<int> Handle(CreateProductCmommand request, CancellationToken cancellationToken)
        {
            Product product = new Product(request.ProductDto.Name, request.ProductDto.Price, request.ProductDto.Stock);
            db.Products.Add(product);
            await db.SaveEntitiesAsync(cancellationToken);

            const string cacheKey = "products:all";
            await cache.RemoveAsync(cacheKey, cancellationToken);

            return product.Id;
        }
    }
}
