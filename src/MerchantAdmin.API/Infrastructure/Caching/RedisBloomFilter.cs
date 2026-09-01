using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using IDatabase = StackExchange.Redis.IDatabase;

namespace MerchantAdmin.API.Infrastructure.Caching;

/// <summary>
/// 基于 Redis 位图（SETBIT/GETBIT）的布隆过滤器，支持跨进程共享。
/// 位图大小 bitSize 默认 100 万位（约 125KB），hashCount 默认 7，
/// 可容纳约 10 万元素且误判率在 1% 以内（可按数据量调参）。
/// </summary>
public sealed class RedisBloomFilter : IBloomFilter
{
    private readonly IDatabase _db;
    private readonly long _bitSize;
    private readonly int _hashCount;

    public RedisBloomFilter(IRedisConnectionProvider provider, long bitSize = 1_000_000, int hashCount = 7)
    {
        _db = provider.Connection.GetDatabase();
        _bitSize = bitSize;
        _hashCount = hashCount;
    }

    public async Task AddAsync(string setName, string element, CancellationToken ct = default)
    {
        var bitKey = "bloom:" + setName;
        foreach (var pos in GetPositions(element))
        {
            await _db.StringSetBitAsync(bitKey, pos, true);
        }
    }

    public async Task AddAsync(string setName, IEnumerable<string> elements, CancellationToken ct = default)
    {
        var bitKey = "bloom:" + setName;
        var batch = _db.CreateBatch();
        foreach (var element in elements)
        {
            foreach (var pos in GetPositions(element))
            {
                _ = batch.StringSetBitAsync(bitKey, pos, true);
            }
        }
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task<bool> ContainsAsync(string setName, string element, CancellationToken ct = default)
    {
        var bitKey = "bloom:" + setName;
        foreach (var pos in GetPositions(element))
        {
            if (!await _db.StringGetBitAsync(bitKey, pos))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>双哈希展开成 hashCount 个位位置：h_i = h1 + i*h2。</summary>
    private IEnumerable<long> GetPositions(string element)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(element));
        var h1 = BitConverter.ToUInt32(hash, 0);
        var h2 = BitConverter.ToUInt32(hash, 4);
        for (var i = 0; i < _hashCount; i++)
        {
            yield return (h1 + i * h2) % _bitSize;
        }
    }
}
