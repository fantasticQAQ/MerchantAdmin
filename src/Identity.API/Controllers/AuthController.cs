using System.Security.Claims;

namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IRefreshTokenService _refreshTokens;
        private readonly ICredentialRevoker _revoker;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService,
            IRefreshTokenService refreshTokens,
            ICredentialRevoker revoker)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _refreshTokens = refreshTokens;
            _revoker = revoker;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest req)
        {
            // 邮箱可选：未填写时保持为 null
            var user = new ApplicationUser(req.UserName, req.Email);
            var result = await _userManager.CreateAsync(user, req.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // 新注册用户默认赋予管理员角色
            var addRoleResult = await _userManager.AddToRoleAsync(user, "Admin");
            if (!addRoleResult.Succeeded)
                return BadRequest(addRoleResult.Errors);

            return Ok("注册成功");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest req)
        {
            var user = await _userManager.FindByNameAsync(req.UserName);
            if (user == null)
                return Unauthorized(new { message = "用户名或密码错误" });

            var ok = await _userManager.CheckPasswordAsync(user, req.Password);
            if (!ok)
                return Unauthorized(new { message = "用户名或密码错误" });

            return Ok(await IssueTokenPairAsync(user));
        }

        /// <summary>
        /// 用 refresh token 换一对新的 token。
        /// 这是整条链路里唯一会重新读库的地方：用户是否还在、SecurityStamp 是否变过、当前角色是什么，
        /// 全部在此刻重新计算——原先分散在每个请求上的校验，现在集中到了这里。
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshTokenRequest req)
        {
            var user = await _refreshTokens.ConsumeAsync(req.RefreshToken);
            if (user is null)
            {
                return Unauthorized(new { message = "登录状态已失效，请重新登录" });
            }

            return Ok(await IssueTokenPairAsync(user));
        }

        private async Task<TokenPairResponse> IssueTokenPairAsync(ApplicationUser user)
        {
            var token = await _tokenService.CreateAccessTokenAsync(user);
            var refreshToken = await _refreshTokens.IssueAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            return new TokenPairResponse(token, refreshToken, user.UserName!, roles.ToList());
        }

        // 个人中心：修改自己的密码
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId!);
            if (user is null)
            {
                return NotFound("用户不存在");
            }

            var result = await _userManager.ChangePasswordAsync(user, req.OldPassword, req.NewPassword);
            if (!result.Succeeded)
            {
                // 转为友好的中文错误提示
                var error = result.Errors.FirstOrDefault();
                var message = error?.Code == "PasswordMismatch"
                    ? "原密码错误"
                    : error?.Description ?? "密码修改失败";
                return BadRequest(new { message });
            }

            // 撤销该用户所有已签发的 token：其他设备下一次请求就会被拒，续期也会失败（SecurityStamp 已变）。
            // 当前设备手上的 access token 会在续期失败后被踢出。
            await _revoker.RevokeAsync(user);

            return Ok("密码修改成功");
        }

        // 获取当前登录用户信息（含最新角色），用于前端角色变更后即时同步 UI
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId!);
            if (user is null)
            {
                return NotFound("用户不存在");
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { userName = user.UserName, roles });
        }
    }

    public record ChangePasswordRequest(string OldPassword, string NewPassword);

    public record RefreshTokenRequest(string RefreshToken);

    /// <summary>登录 / 刷新成功时返回的一对 token。</summary>
    public record TokenPairResponse(string Token, string RefreshToken, string UserName, List<string> Roles);
}
