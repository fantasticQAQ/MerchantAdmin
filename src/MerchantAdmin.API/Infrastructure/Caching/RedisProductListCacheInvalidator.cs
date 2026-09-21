namespace MerchantAdmin.API.Infrastructure.Caching;

public sealed class RedisProductListCacheInvalidator(IRedisConnectionProvider provider) : IProductListCacheInvalidator
{
    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await provider.Connection.GetDatabase().StringIncrementAsync(ProductCacheKeys.VersionKey);
    }
}
