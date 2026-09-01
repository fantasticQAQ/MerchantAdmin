using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace MerchantAdmin.API.Infrastructure.Caching
{
    public interface IRedisConnectionProvider
    {
        IConnectionMultiplexer Connection { get; }
    }

    /// <summary>
    /// Redis 连接提供者：
    /// - ConnectionMultiplexer 内置连接池（复用一个底层连接，不要每次 new）；
    /// - AbortOnConnectFail=false：Redis 暂时不可用时进程不崩溃，稍后自动重连；
    /// - ReconnectRetryPolicy 指数退避重连；
    /// - 监听 ConnectionFailed / ConnectionRestored 事件做日志告警。
    /// </summary>
    public sealed class RedisConnectionProvider : IRedisConnectionProvider, IDisposable
    {
        public IConnectionMultiplexer Connection { get; }

        public RedisConnectionProvider(IConfiguration configuration, ILogger<RedisConnectionProvider> logger)
        {
            var options = ConfigurationOptions.Parse(
                configuration["Redis:ConnectionString"] ?? "localhost:6379");

            options.AbortOnConnectFail = false;   // Redis 不可用时不崩溃，启动后自动重连
            options.ConnectRetry = 5;             // 首次连接重试次数
            options.ConnectTimeout = 5000;        // 连接超时（毫秒）
            options.KeepAlive = 60;               // 心跳保活（秒）
            options.SyncTimeout = 5000;           // 同步命令超时
            options.AsyncTimeout = 5000;          // 异步命令超时
            options.ReconnectRetryPolicy = new ExponentialRetry(5000); // 断线指数退避重连（初始 5s，逐次加倍）

            Connection = ConnectionMultiplexer.Connect(options);

            Connection.ConnectionFailed += (_, e) =>
                logger.LogWarning("Redis 连接失败: {Type} {EndPoint} {Error}",
                    e.ConnectionType, e.EndPoint, e.Exception?.Message);
            Connection.ConnectionRestored += (_, e) =>
                logger.LogInformation("Redis 连接恢复: {Type} {EndPoint}",
                    e.ConnectionType, e.EndPoint);
        }

        public void Dispose() => Connection.Dispose();
    }
}
