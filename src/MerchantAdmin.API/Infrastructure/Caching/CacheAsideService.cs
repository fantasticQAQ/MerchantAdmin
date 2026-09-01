using System.Text.Json;
using IDatabase = StackExchange.Redis.IDatabase;

namespace MerchantAdmin.API.Infrastructure.Caching;

/// <summary>
/// 组合缓存服务实现：
/// 1. 读：缓存命中直接返回 → 未命中加分布式锁查库回填（防击穿）；
/// 2. DB 返回 null 时缓存空值（短过期，防穿透）；
/// 3. TTL 加随机偏移（防雪崩）；
/// 4. 写：延时双删（先删缓存 → 更新 DB → 延时再删），防止并发读把旧数据回填。
/// </summary>
public sealed class CacheAsideService : ICacheAsideService
{
    private readonly IDatabase _db;
    private readonly IDistributedLock _lock;

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NullValueTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DoubleDeleteDelay = TimeSpan.FromMilliseconds(500);
    private const string LockPrefix = "cache:lock:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CacheAsideService(IRedisConnectionProvider provider, IDistributedLock distributedLock)
    {
        _db = provider.Connection.GetDatabase();
        _lock = distributedLock;
    }

    public async Task<T?> GetOrAddAsync<T>(
        string cacheKey,
        Func<Task<T>> dbQuery,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        // 1. 查缓存
        var cached = await _db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<T>(cached!, JsonOptions);
        }

        // 2. 分布式锁：同一 key 的并发只让一个线程查库回填（防击穿）
        return await _lock.ExecuteWithLockAsync(
            LockPrefix + cacheKey,
            async () =>
            {
                // double-check：拿到锁后再查一次缓存，避免重复查库
                var again = await _db.StringGetAsync(cacheKey);
                if (again.HasValue)
                {
                    return JsonSerializer.Deserialize<T>(again!, JsonOptions);
                }

                var data = await dbQuery();
                if (data is not null)
                {
                    await _db.StringSetAsync(cacheKey, JsonSerializer.Serialize(data, JsonOptions), RandomTtl(ttl));
                }
                else
                {
                    // 缓存空值（短过期）：防穿透兜底，避免不存在的数据反复打库
                    await _db.StringSetAsync(cacheKey, "null", NullValueTtl);
                }
                return data;
            },
            expiry: TimeSpan.FromSeconds(10),
            acquireTimeout: TimeSpan.FromSeconds(5),
            ct);
    }

    public async Task UpdateWithDoubleDeleteAsync(string cacheKey, Func<Task> updateDb, CancellationToken ct = default)
    {
        // 第一次删缓存
        await _db.KeyDeleteAsync(cacheKey);
        // 更新数据库
        await updateDb();
        // 延时第二次删缓存：防并发读线程把"删缓存前"的旧数据回填到缓存
        _ = FireAndForgetDoubleDeleteAsync(cacheKey);
    }

    public async Task RemoveWithDoubleDeleteAsync(string cacheKey, CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(cacheKey);
        _ = FireAndForgetDoubleDeleteAsync(cacheKey);
    }

    private async Task FireAndForgetDoubleDeleteAsync(string cacheKey)
    {
        try
        {
            await Task.Delay(DoubleDeleteDelay);
            await _db.KeyDeleteAsync(cacheKey);
        }
        catch
        {
            // 双删失败不影响主流程（缓存会随 TTL 过期兜底）
        }
    }

    /// <summary>雪崩防护：TTL 加 0~300 秒随机偏移，避免大量 key 同一时间点集中过期。</summary>
    private static TimeSpan RandomTtl(TimeSpan? ttl)
    {
        var baseTtl = ttl ?? DefaultTtl;
        return baseTtl + TimeSpan.FromSeconds(Random.Shared.Next(0, 300));
    }
}
