using StackExchange.Redis;
using IDatabase = StackExchange.Redis.IDatabase;

namespace MerchantAdmin.API.Infrastructure.Caching
{
    public sealed class RedisDelayJobService : IDelayJobService
    {
        private readonly IDatabase _db;

        public RedisDelayJobService(IRedisConnectionProvider provider)
        {
            _db = provider.Connection.GetDatabase();
        }

        private static string Key(int orderId) => $"order:cancel:{orderId}";

        public async Task ScheduleCancelOrderAsync(int orderId, TimeSpan delay)
        {
            await _db.StringSetAsync(Key(orderId), "1", delay);
        }

        public async Task CancelCancelOrderAsync(int orderId)
        {
            await _db.KeyDeleteAsync(Key(orderId));
        }
    }
}
