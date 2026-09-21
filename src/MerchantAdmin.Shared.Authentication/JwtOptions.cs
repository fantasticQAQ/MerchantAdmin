namespace MerchantAdmin.Shared.Authentication;

/// <summary>
/// JWT 配置项，对应 appsettings.json 中的 "Jwt" 节点。
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// access token 有效期（分钟）。资源服务不再做任何外部校验，撤销只能靠 token 自然过期，
    /// 所以这个值同时就是「改密码后旧 token 还能用多久」的窗口，不能设太长。
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>refresh token 有效期（天）。刷新时才会重新读库，因此角色变更也在这一刻生效。</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
