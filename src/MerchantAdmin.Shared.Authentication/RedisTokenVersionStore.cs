using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MerchantAdmin.Shared.Authentication;

/// <summary>
/// 基于 Redis 的凭证版本号存储。
/// <para>
/// 只存一个整数，所以单次读取是内存级的（内网约 0.1ms）——正因为它足够便宜，
/// 才不需要在资源服务里加本地缓存，也就没有了"缓存 TTL 就是撤销窗口"的问题。
/// </para>
/// </summary>
public sealed class RedisTokenVersionStore : ITokenVersionStore
{
    /// <summary>
    /// 单次读取的时间上限。认证是每个请求的必经之路，读的又是内网 Redis，
    /// 所以宁可保守一点，也不能让 Redis 故障拖慢整站。超时按"无撤销信息"处理（放行）。
    /// </summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(200);

    private const string KeyPrefix = "auth:ver:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTokenVersionStore> _logger;

    public RedisTokenVersionStore(IConnectionMultiplexer redis, ILogger<RedisTokenVersionStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<long?> GetAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var value = await _redis.GetDatabase()
                .StringGetAsync(Key(userId))
                .WaitAsync(ReadTimeout, ct);

            return value.HasValue && long.TryParse(value.ToString(), out var version) ? version : null;
        }
        catch (Exception ex)
        {
            // fail-open：Redis 不可用时放行，撤销能力暂时退回"等 access token 过期"。
            // 这是刻意的选择——失败方向应该是"撤销暂时失效"，而不是"Redis 一抖全站被锁在门外"。
            _logger.LogWarning(ex, "读取凭证版本号失败，本次跳过撤销校验（userId={UserId}）", userId);
            return null;
        }
    }

    public async Task SetAsync(long userId, long version, CancellationToken ct = default)
    {
        try
        {
            await _redis.GetDatabase().StringSetAsync(Key(userId), version);
        }
        catch (Exception ex)
        {
            // 不向上抛：调用方此时已经改完数据库，抛异常只会让管理员看到一个自相矛盾的失败。
            // 但必须记 Error——这意味着这次撤销没有立即生效，需要人知道。
            _logger.LogError(ex,
                "写入凭证版本号失败，该用户的撤销不会立即生效（userId={UserId}, version={Version}）",
                userId, version);
        }
    }

    private static string Key(long userId) => $"{KeyPrefix}{userId}";
}

public static class TokenVersionStoreExtensions
{
    /// <summary>注册基于 Redis 的凭证版本号存储。</summary>
    /// <param name="redisConnectionString">
    /// Redis 连接串。传 null 表示该服务已经注册了 <see cref="IConnectionMultiplexer"/>，
    /// 直接复用它的连接，避免为同一个 Redis 建两条连接。
    /// </param>
    public static IServiceCollection AddRedisTokenVersionStore(
        this IServiceCollection services,
        string? redisConnectionString = null)
    {
        if (redisConnectionString is not null)
        {
            // 用工厂延迟到首次解析时再连，Redis 不可用也不会拖垮服务启动
            services.AddSingleton<IConnectionMultiplexer>(_ => Connect(redisConnectionString));
        }

        services.AddSingleton<ITokenVersionStore, RedisTokenVersionStore>();
        return services;
    }

    private static IConnectionMultiplexer Connect(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;                          // Redis 暂时不可用时不崩溃，连上后自动重连
        options.ConnectRetry = 5;
        options.ReconnectRetryPolicy = new ExponentialRetry(5000);
        return ConnectionMultiplexer.Connect(options);
    }
}
