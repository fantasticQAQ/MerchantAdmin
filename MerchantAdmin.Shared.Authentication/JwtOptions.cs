namespace MerchantAdmin.Shared.Authentication;

/// <summary>
/// JWT 配置项，对应 appsettings.json 中的 "Jwt" 节点。
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}
