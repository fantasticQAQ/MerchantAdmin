using MerchantAdmin.API.Infrastructure.Caching;
using MerchantAdmin.Domain.Entities.AggregatesModel;

namespace MerchantAdmin.API.Application.Commands
{
    public class CreateProductCommandHandler(AppDbContext db, ICacheService cache) : IRequestHandler<CreateProductCommand, int>
    {
        public async Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var product = new Product(request.ProductDto.Name, request.ProductDto.Price, request.ProductDto.Stock);
            db.Products.Add(product);
            await db.SaveEntitiesAsync(cancellationToken);

            const string cacheKey = "products:all";
            await cache.RemoveAsync(cacheKey, cancellationToken);

            return product.Id;
        }
    }
}
