using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.API.Application.Commands
{
    public class DeleteProductCommandHandler(AppDbContext db, ICacheService cache) : IRequestHandler<DeleteProductCommand, bool>
    {
        public async Task<bool> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
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
