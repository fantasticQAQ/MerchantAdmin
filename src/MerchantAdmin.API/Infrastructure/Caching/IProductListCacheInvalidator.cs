namespace MerchantAdmin.API.Infrastructure.Caching;

/// <summary>商品列表按版本号失效：INCR 后旧 list key 自然作废。</summary>
public interface IProductListCacheInvalidator
{
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}

public static class ProductCacheKeys
{
    public const string VersionKey = "product:version";
}
