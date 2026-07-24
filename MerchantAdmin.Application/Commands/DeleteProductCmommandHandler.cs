using MediatR;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;

namespace MerchantAdmin.Application.Commands
{
    public class DeleteProductCmommandHandler(AppDbContext db, ICacheService cache) : IRequestHandler<DeleteProductCmommand, bool>
    {
        public async Task<bool> Handle(DeleteProductCmommand request, CancellationToken cancellationToken)
        {
            var product = await db.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                return false;
            }

            db.Products.Remove(product);
            await db.SaveEntitiesAsync(cancellationToken);

            const string cacheKey = "products:all";
            await cache.RemoveAsync(cacheKey, cancellationToken);

            return true;
        }
    }
}
