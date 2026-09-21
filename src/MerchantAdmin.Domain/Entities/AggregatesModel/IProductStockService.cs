namespace MerchantAdmin.Domain.Entities.AggregatesModel;

/// <summary>商品库存持久化：关系库走条件 UPDATE，避免读改写丢失更新。</summary>
public interface IProductStockService
{
    Task DecreaseAsync(int productId, decimal quantity, CancellationToken cancellationToken = default);
    Task IncreaseAsync(int productId, decimal quantity, CancellationToken cancellationToken = default);
}
