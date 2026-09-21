using MerchantAdmin.Domain.Entities.AggregatesModel;

namespace MerchantAdmin.API.Application.Commands
{
    /// <summary>
    /// 注意：商品列表缓存的失效**不在这里做**。本 handler 运行在 TransactionBehavior 的事务内，
    /// 此时商品行锁仍被持有，Redis 往返会拉长持锁时间（压测中这是单行热点的瓶颈之一）。
    /// 失效由调用方 ProductsController.Update 在事务提交后执行。
    /// </summary>
    public class UpdateProductCommandHandler(
        AppDbContext db,
        IProductStockService stock)
        : IRequestHandler<UpdateProductCommand, bool>
    {
        public async Task<bool> Handle(UpdateProductCommand request, CancellationToken ct)
        {
            var product = await db.Products.FindAsync(request.ProductId);
            if (product is null)
            {
                return false;
            }

            if (request.StockDelta.HasValue)
            {
                var delta = request.StockDelta.Value;
                if (delta > 0)
                    await stock.IncreaseAsync(product.Id, delta, ct);
                else if (delta < 0)
                    await stock.DecreaseAsync(product.Id, -delta, ct);
            }

            if (request.Name is not null || request.Price.HasValue)
            {
                product.UpdateInfo(request.Name ?? product.Name, request.Price ?? product.Price);
            }

            if (request.IsActive.HasValue)
            {
                product.SetActive(request.IsActive.Value);
            }

            await db.SaveEntitiesAsync(ct);

            return true;
        }
    }
}
