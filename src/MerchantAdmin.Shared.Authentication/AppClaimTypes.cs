namespace MerchantAdmin.Shared.Authentication;

/// <summary>本项目自定义的 claim 名称。</summary>
public static class AppClaimTypes
{
    /// <summary>
    /// 凭证版本号。token 里固化的版本号与 Redis 中的当前值不一致，即说明该 token 已被撤销。
    /// 由 <c>Identity.API</c> 签发，由 <see cref="JwtAuthenticationExtensions"/> 校验。
    /// </summary>
    public const string TokenVersion = "ver";
}
