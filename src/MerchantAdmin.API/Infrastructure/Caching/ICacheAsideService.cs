namespace MerchantAdmin.API.Infrastructure.Caching;

/// <summary>
/// 组合缓存服务（Cache-Aside 增强版）：
/// 分布式锁（防击穿）+ 延时双删（防脏读）+ 空值缓存/随机 TTL（防穿透/防雪崩）。
/// 布隆过滤器（<see cref="IBloomFilter"/>）作为独立组件，由调用方在"按 ID 查详情"等场景前置使用。
/// </summary>
public interface ICacheAsideService
{
    /// <summary>
    /// 读缓存：缓存命中直接返回；未命中加分布式锁查库回填（防击穿）。
    /// DB 返回 null 时缓存空值（短过期，防穿透兜底）；TTL 自动加随机偏移（防雪崩）。
    /// </summary>
    Task<T?> GetOrAddAsync<T>(
        string cacheKey,
        Func<Task<T>> dbQuery,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>写操作：先删缓存 → 更新数据库 → 延时再删一次（延时双删，防脏读）。</summary>
    Task UpdateWithDoubleDeleteAsync(string cacheKey, Func<Task> updateDb, CancellationToken ct = default);

    /// <summary>删除：延时双删。</summary>
    Task RemoveWithDoubleDeleteAsync(string cacheKey, CancellationToken ct = default);
}
