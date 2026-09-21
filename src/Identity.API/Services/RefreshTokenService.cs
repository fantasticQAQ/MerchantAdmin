using System.Security.Cryptography;
using System.Text;
using Identity.API.Data;
using MerchantAdmin.Shared.Authentication;
using Microsoft.Extensions.Options;

namespace Identity.API.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        /// <summary>
        /// 判定「良性竞态」的宽限时间：同一浏览器多个标签页各自持有一份刷新逻辑，
        /// 可能几乎同时拿同一个 refresh token 来换（一个成功、另一个撞上刚作废的记录）。
        /// 这个窗口内只拒绝、不作废整条链，客户端读完最新 token 重试即可。
        /// 超过窗口仍被使用，才按泄露处理。
        /// </summary>
        private static readonly TimeSpan RaceGrace = TimeSpan.FromSeconds(30);

        private readonly IdentityDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtOptions _jwt;
        private readonly ILogger<RefreshTokenService> _logger;

        public RefreshTokenService(
            IdentityDbContext db,
            UserManager<ApplicationUser> userManager,
            IOptions<JwtOptions> jwt,
            ILogger<RefreshTokenService> logger)
        {
            _db = db;
            _userManager = userManager;
            _jwt = jwt.Value;
            _logger = logger;
        }

        public async Task<string> IssueAsync(ApplicationUser user, CancellationToken ct = default)
        {
            var plain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var now = DateTime.UtcNow;

            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = Hash(plain),
                SecurityStamp = user.SecurityStamp ?? string.Empty,
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwt.RefreshTokenDays)
            });
            await _db.SaveChangesAsync(ct);

            return plain;
        }

        public async Task<ApplicationUser?> ConsumeAsync(string refreshToken, CancellationToken ct = default)
        {
            var row = await _db.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), ct);

            if (row is null)
            {
                return null;
            }

            if (row.RevokedAt is not null)
            {
                // 已作废的 token 又被拿来用：可能是多标签页竞态，也可能是 refresh token 泄露。
                // 刚作废的按竞态处理——此时真正的风险是「客户端被莫名踢出登录」，而不是安全；
                // 具体见 RaceGrace 的说明。
                if (DateTime.UtcNow - row.RevokedAt.Value >= RaceGrace)
                {
                    _logger.LogWarning(
                        "refresh token 被重复使用，判定为泄露，作废用户 {UserId} 的全部 refresh token", row.UserId);
                    await RevokeAllAsync(row.UserId, ct);
                }
                return null;
            }

            if (row.ExpiresAt <= DateTime.UtcNow)
            {
                return null;
            }

            var user = await _userManager.FindByIdAsync(row.UserId.ToString());
            if (user is null)
            {
                return null;
            }

            // 改过密码（SecurityStamp 变了）→ 旧 refresh token 一律失效。
            // 这是原先把「每个请求回查身份服务」搬到刷新时之后的等价保障。
            if (!string.Equals(row.SecurityStamp, user.SecurityStamp ?? string.Empty, StringComparison.Ordinal))
            {
                row.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                return null;
            }

            // 轮换：这张一次性用完即废，调用方随后签发新的一对
            row.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return user;
        }

        public async Task RevokeAllAsync(long userId, CancellationToken ct = default)
        {
            var tokens = await _db.RefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ToListAsync(ct);

            if (tokens.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var token in tokens)
            {
                token.RevokedAt = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        private static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
