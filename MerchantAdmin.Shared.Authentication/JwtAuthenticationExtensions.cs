using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace MerchantAdmin.Shared.Authentication;

/// <summary>
/// JWT 认证统一配置。
/// 只负责「校验」：签名校验 + SecurityStamp 校验 + 实时角色刷新。
/// 用户安全信息（SecurityStamp/角色）通过 <see cref="ITokenUserProvider"/> 抽象获取，
/// 认证库不直接依赖数据库。签发（签名能力）仍留在身份服务（Identity.API）。
/// </summary>
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddAppJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("缺少 Jwt 配置节点");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidIssuer = jwt.Issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine("❌ JWT 校验失败：" + context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var securityStamp = context.Principal?.FindFirstValue("securityStamp");

                    if (userId is null || !long.TryParse(userId, out var userIdLong))
                    {
                        return;
                    }

                    // 通过抽象接口获取最新安全信息；未注册时仅做签名校验
                    var provider = context.HttpContext.RequestServices.GetService<ITokenUserProvider>();
                    if (provider is null)
                    {
                        return;
                    }

                    try
                    {
                        var info = await provider.GetAsync(userIdLong);
                        if (info is null || info.SecurityStamp != securityStamp)
                        {
                            context.Fail("安全凭证已变更，请重新登录");
                            return;
                        }

                        // 用最新角色替换 token 里固化的旧角色，让 [Authorize(Roles=...)] 使用实时角色
                        var identity = (ClaimsIdentity)context.Principal!.Identity!;
                        var staleRoles = identity.FindAll(ClaimTypes.Role).ToList();
                        foreach (var c in staleRoles)
                        {
                            identity.RemoveClaim(c);
                        }
                        foreach (var role in info.Roles)
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }
                    }
                    catch
                    {
                        // 用户信息提供方异常时跳过，避免影响（例如集成测试环境）
                    }
                }
            };
        });

        services.AddAuthorization();
        return services;
    }
}
