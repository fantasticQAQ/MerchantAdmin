using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Exceptions;

namespace MerchantAdmin.Infrastructure.Services;

public sealed class ProductStockService(AppDbContext db) : IProductStockService
{
    public async Task DecreaseAsync(int productId, decimal quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new DomainException("数量必须大于0");

        if (db.Database.IsRelational())
        {
            var rows = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET Stock = Stock - {quantity} WHERE Id = {productId} AND Stock >= {quantity}",
                cancellationToken);

            if (rows == 0)
                throw new DomainException("库存不足");

            await ReloadTrackedProductAsync(productId, cancellationToken);
            return;
        }

        var product = await db.Products.FindAsync([productId], cancellationToken)
            ?? throw new DomainException("商品不存在");
        product.ReduceStock(quantity);
    }

    public async Task IncreaseAsync(int productId, decimal quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) return;

        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET Stock = Stock + {quantity} WHERE Id = {productId}",
                cancellationToken);

            await ReloadTrackedProductAsync(productId, cancellationToken);
            return;
        }

        var product = await db.Products.FindAsync([productId], cancellationToken);
        product?.IncreaseStock(quantity);
    }

    private async Task ReloadTrackedProductAsync(int productId, CancellationToken cancellationToken)
    {
        var tracked = db.ChangeTracker.Entries<Product>()
            .FirstOrDefault(e => e.Entity.Id == productId);
        if (tracked is not null)
            await tracked.ReloadAsync(cancellationToken);
    }
}
