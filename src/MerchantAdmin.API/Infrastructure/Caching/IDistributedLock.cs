namespace MerchantAdmin.API.Infrastructure.Caching;

/// <summary>基于 Redis 的分布式锁。</summary>
public interface IDistributedLock
{
    /// <summary>尝试获取锁；成功返回 token（释放锁时使用），失败返回 null。</summary>
    Task<string?> TryAcquireAsync(string lockKey, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>释放锁（校验 token，防止误删他人的锁）。</summary>
    Task<bool> ReleaseAsync(string lockKey, string token, CancellationToken ct = default);

    /// <summary>
    /// 在锁内执行 action：获取锁（带等待重试）→ 执行 → 释放。
    /// 在 acquireTimeout 内获取不到锁则返回 default。
    /// </summary>
    Task<T?> ExecuteWithLockAsync<T>(
        string lockKey,
        Func<Task<T?>> action,
        TimeSpan? expiry = null,
        TimeSpan? acquireTimeout = null,
        CancellationToken ct = default);
}
