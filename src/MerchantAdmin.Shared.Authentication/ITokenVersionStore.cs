namespace MerchantAdmin.Shared.Authentication;

/// <summary>
/// 凭证版本号存储。
/// <para>
/// 写端是身份服务：改密码、重置密码、改角色、删账号时把该用户的版本号 +1 写进来。
/// 读端是各资源服务：校验 token 时拿 token 里的 <c>ver</c> 与这里比对，不一致就拒绝。
/// </para>
/// <para>
/// 这是"撤销立即生效"的落点——资源服务不查库、不调用身份服务，只做一次内存级键值读，
/// 所以撤销窗口从"access token 的有效期"压到了 Redis 的一次往返。
/// </para>
/// </summary>
public interface ITokenVersionStore
{
    /// <summary>
    /// 读取用户当前的凭证版本号。
    /// 返回 null 表示"没有撤销信息可用"，既包括该用户从未撤销过，也包括 Redis 不可用。
    /// 调用方应据此放行（fail-open）。
    /// </summary>
    Task<long?> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>写入用户最新的凭证版本号。</summary>
    Task SetAsync(long userId, long version, CancellationToken ct = default);
}
