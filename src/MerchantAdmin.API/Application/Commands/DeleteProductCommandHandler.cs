using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.API.Application.Commands
{
    public class DeleteProductCommandHandler(AppDbContext db, IProductListCacheInvalidator productListCache)
        : IRequestHandler<DeleteProductCommand, bool>
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

            await productListCache.InvalidateAsync(cancellationToken);

            return true;
        }
    }
}
