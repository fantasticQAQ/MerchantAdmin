using StackExchange.Redis;
using IDatabase = StackExchange.Redis.IDatabase;

namespace MerchantAdmin.API.Infrastructure.Caching;

/// <summary>
/// Redis 分布式锁：
/// - 加锁：SET key token NX PX（原子），token 随机串防止误删；
/// - 释放：Lua 校验 token 后才删除，防止误删其他持有者的锁；
/// - 可重入：同一异步流内重复加锁计数累加，释放时计数归零才真正删除；
/// - 看门狗：后台定时续期，避免业务执行时间超过锁 TTL 导致锁被提前释放。
/// 已知局限：Redis 单点故障时锁不可用（可升级 RedLock）；极端情况下仍可能锁过期失效。
/// </summary>
public sealed class RedisDistributedLock : IDistributedLock
{
    private readonly IDatabase _db;
    private readonly AsyncLocal<Dictionary<string, (string Token, int Count)>> _heldLocks = new();

    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(10);
    private const int RetryIntervalMs = 50;

    // Lua：仅当 value 匹配时才删除，防止误删其他线程/进程的锁
    private const string ReleaseScript = """
        if redis.call("get", KEYS[1]) == ARGV[1] then
            return redis.call("del", KEYS[1])
        else
            return 0
        end
        """;

    public RedisDistributedLock(IRedisConnectionProvider provider)
    {
        _db = provider.Connection.GetDatabase();
    }

    public async Task<string?> TryAcquireAsync(string lockKey, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var held = GetHeld();

        // 可重入：当前异步流已持有该锁 → 计数 +1，返回原 token（锁不重置）
        if (held.TryGetValue(lockKey, out var existing))
        {
            held[lockKey] = (existing.Token, existing.Count + 1);
            return existing.Token;
        }

        var token = Guid.NewGuid().ToString("N");
        var ok = await _db.StringSetAsync(lockKey, token, expiry ?? DefaultExpiry, When.NotExists);
        if (ok)
        {
            held[lockKey] = (token, 1);
        }
        return ok ? token : null;
    }

    public async Task<bool> ReleaseAsync(string lockKey, string token, CancellationToken ct = default)
    {
        var held = GetHeld();

        // 可重入：计数 > 1 只减计数，真正归零才删除 Redis 锁
        if (held.TryGetValue(lockKey, out var existing))
        {
            if (existing.Count > 1)
            {
                held[lockKey] = (existing.Token, existing.Count - 1);
                return true;
            }
            held.Remove(lockKey);
        }

        var result = await _db.ScriptEvaluateAsync(
            ReleaseScript,
            new RedisKey[] { lockKey },
            new RedisValue[] { token });
        return (long)result == 1;
    }

    public async Task<T?> ExecuteWithLockAsync<T>(
        string lockKey,
        Func<Task<T?>> action,
        TimeSpan? expiry = null,
        TimeSpan? acquireTimeout = null,
        CancellationToken ct = default)
    {
        var lockExpiry = expiry ?? DefaultExpiry;
        var deadline = DateTime.UtcNow + (acquireTimeout ?? TimeSpan.FromSeconds(5));
        string? token;
        while ((token = await TryAcquireAsync(lockKey, lockExpiry, ct)) is null)
        {
            if (DateTime.UtcNow >= deadline)
            {
                return default;
            }
            await Task.Delay(RetryIntervalMs, ct);
        }

        // 看门狗：后台定时续期，防止业务执行超过锁 TTL
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var renewal = RenewAsync(lockKey, token, lockExpiry, cts.Token);

        try
        {
            return await action();
        }
        finally
        {
            cts.Cancel();
            try { await renewal; } catch { /* 续期任务随取消结束 */ }
            await ReleaseAsync(lockKey, token, ct);
        }
    }

    private async Task RenewAsync(string lockKey, string token, TimeSpan expiry, CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(expiry.TotalMilliseconds / 3);
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(interval, ct);
            // 仅当锁仍归自己（key 存在）时续期；When.Exists 避免续到别人新获取的锁
            var ok = await _db.StringSetAsync(lockKey, token, expiry, When.Exists);
            if (!ok)
            {
                return; // 锁已丢失，停止续期
            }
        }
    }

    private Dictionary<string, (string Token, int Count)> GetHeld()
    {
        _heldLocks.Value ??= new Dictionary<string, (string, int)>();
        return _heldLocks.Value;
    }
}
