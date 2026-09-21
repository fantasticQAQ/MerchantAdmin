using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MerchantAdmin.Shared.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.API.Services
{
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _jwt;
        private readonly UserManager<ApplicationUser> _userManager;

        public TokenService(IOptions<JwtOptions> jwt, UserManager<ApplicationUser> userManager)
        {
            _jwt = jwt.Value;
            _userManager = userManager;
        }

        public async Task<string> CreateAccessTokenAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                // 凭证版本号：资源服务用它和 Redis 里的当前值比对，判断这张 token 是否已被撤销
                new Claim(AppClaimTypes.TokenVersion, user.TokenVersion.ToString()),
            };

            // 角色直接固化进 token，资源服务据此做 [Authorize(Roles = "...")]，
            // 不再回查身份服务或数据库。角色变更会在下一次刷新时生效。
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
                claims: claims,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
