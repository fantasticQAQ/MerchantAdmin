namespace MerchantAdmin.Shared.Authentication;

/// <summary>
/// 提供 token 校验所需的用户安全信息（SecurityStamp + 最新角色）。
/// 认证库只依赖此抽象、不直接依赖数据库；各服务按自己的数据访问方式实现。
/// </summary>
public interface ITokenUserProvider
{
    Task<TokenUserInfo?> GetAsync(long userId);
}

/// <summary>token 校验所需的用户安全信息。</summary>
public sealed record TokenUserInfo(string SecurityStamp, IReadOnlyList<string> Roles);
