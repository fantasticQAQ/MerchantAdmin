using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.API.Application.Commands
{
    public class UpdateProductCommandHandler(AppDbContext db, ICacheService cache) : IRequestHandler<UpdateProductCommand, bool>
    {
        public async Task<bool> Handle(UpdateProductCommand request, CancellationToken ct)
        {
            var product = await db.Products.FindAsync(request.ProductId);
            if (product is null)
            {
                return false;
            }

            // 编辑名称/价格
            if (request.Name is not null || request.Price.HasValue)
            {
                product.UpdateInfo(request.Name ?? product.Name, request.Price ?? product.Price);
            }

            // 调整库存（正数补货，负数扣减）
            if (request.StockDelta.HasValue)
            {
                product.AdjustStock(request.StockDelta.Value);
            }

            // 上下架
            if (request.IsActive.HasValue)
            {
                product.SetActive(request.IsActive.Value);
            }

            await db.SaveEntitiesAsync(ct);

            // 失效商品缓存
            await cache.RemoveAsync("products:all", ct);

            return true;
        }
    }
}
