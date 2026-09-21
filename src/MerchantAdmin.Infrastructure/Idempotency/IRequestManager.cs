namespace MerchantAdmin.Infrastructure.Idempotency;

/// <summary>
/// 幂等请求管理器：基于数据库表识别重复请求（客户端为每次业务操作生成一个请求 ID，
/// 重发时携带同一 ID，服务端只执行一次）。
/// </summary>
public interface IRequestManager
{
    /// <summary>该请求 ID 是否已处理过。</summary>
    Task<bool> ExistAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 登记请求 ID。只挂到变更跟踪器、不单独提交，
    /// 以便与业务写入在同一事务内一起提交。
    /// </summary>
    /// <returns>true 表示登记成功（新请求）；false 表示该 ID 已存在（重复请求）。</returns>
    Task<bool> CreateRequestForCommandAsync<T>(Guid id, CancellationToken ct = default);
}
