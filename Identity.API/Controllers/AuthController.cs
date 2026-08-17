using System.Security.Claims;
using Identity.API.Dtos;
using Identity.API.Entities;
using Identity.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest req)
        {
            var user = new ApplicationUser(req.UserName, req.Email);
            var result = await _userManager.CreateAsync(user, req.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

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

            var token = await _tokenService.CreateToken(user);
            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new { token, userName = user.UserName, roles });
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

            return Ok("密码修改成功");
        }
    }

    public record ChangePasswordRequest(string OldPassword, string NewPassword);
}
