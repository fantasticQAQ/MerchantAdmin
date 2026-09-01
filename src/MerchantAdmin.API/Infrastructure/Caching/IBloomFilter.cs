namespace MerchantAdmin.API.Infrastructure.Caching;

/// <summary>
/// 布隆过滤器：判断元素"一定不存在"或"可能存在"。
/// 用于缓存穿透防护——查询前先过滤，不存在的 key 直接短路，不查数据库。
/// </summary>
public interface IBloomFilter
{
    Task AddAsync(string setName, string element, CancellationToken ct = default);

    Task AddAsync(string setName, IEnumerable<string> elements, CancellationToken ct = default);

    /// <summary>返回 false 表示元素一定不存在；返回 true 表示可能存在（有误判率）。</summary>
    Task<bool> ContainsAsync(string setName, string element, CancellationToken ct = default);
}
