using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace MerchantAdmin.Infrastructure.Caching
{
    public interface IRedisConnectionProvider
    {
        IConnectionMultiplexer Connection { get; }
    }

    public sealed class RedisConnectionProvider : IRedisConnectionProvider
    {
        public IConnectionMultiplexer Connection { get; }

        public RedisConnectionProvider(IConfiguration configuration)
        {
            Connection = ConnectionMultiplexer.Connect(
                configuration["Redis:ConnectionString"]);
        }
    }
}
