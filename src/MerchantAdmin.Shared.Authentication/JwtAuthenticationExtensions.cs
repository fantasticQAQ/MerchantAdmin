namespace MerchantAdmin.Shared.Authentication;

/// <summary>
/// JWT 认证统一配置：签名校验 + 有效期校验 + 凭证版本号校验。
/// <para>
/// 这里不做数据库查询，也不调用身份服务。撤销通过 Redis 里的一个版本号完成：
/// token 里带着签发时的版本号，与 Redis 中的当前值比对，不一致即拒绝。
/// 单次读取是内存级的，所以不需要本地缓存——也就不会出现"缓存 TTL 就是撤销窗口"的问题。
/// </para>
/// <para>
/// Redis 读不到时放行（fail-open）：失败方向是"撤销暂时失效、退回 access token 的自然过期窗口"，
/// 而不是"Redis 一抖全站被锁在门外"。
/// </para>
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
                    var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (userIdValue is null || !long.TryParse(userIdValue, out var userId))
                    {
                        return;
                    }

                    // 没有 ver 的是本次改造之前签发的 token：跳过校验即可，
                    // 它们最多再存活一个 access token 周期。
                    var verValue = context.Principal?.FindFirstValue(AppClaimTypes.TokenVersion);
                    if (verValue is null || !long.TryParse(verValue, out var tokenVersion))
                    {
                        return;
                    }

                    var store = context.HttpContext.RequestServices.GetService<ITokenVersionStore>();
                    if (store is null)
                    {
                        return;
                    }

                    var currentVersion = await store.GetAsync(userId, context.HttpContext.RequestAborted);
                    if (currentVersion is null)
                    {
                        // 无撤销信息（或 Redis 不可用）→ 放行
                        return;
                    }

                    if (currentVersion.Value != tokenVersion)
                    {
                        // 改密码 / 改角色 / 删账号后，该用户所有已签发的 token 在这一刻失效。
                        // 客户端收到 401 会自动续期：改密码时续期会被拒（被踢下线），
                        // 改角色时续期会拿到新角色（无感换权）。
                        context.Fail("凭证已失效");
                    }
                }
            };
        });

        services.AddAuthorization();
        return services;
    }
}
