using Identity.API.Dtos;
using Identity.Infrastructure.Entities;
using Identity.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

/// <summary>
/// 微信小程序认证
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WxAuthController : ControllerBase
{
    private readonly WxAuthService _wxAuth;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<WxAuthController> _logger;

    public WxAuthController(
        WxAuthService wxAuth,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ILogger<WxAuthController> logger)
    {
        _wxAuth = wxAuth;
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// 微信小程序登录：用 wx.login() 返回的 code 换取 token
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(WxLoginRequest req)
    {
        // 1. 用 code 换取 openid
        var session = await _wxAuth.Code2SessionAsync(req.Code);

        if (!session.IsSuccess)
        {
            _logger.LogWarning("微信 code2session 失败: errcode={ErrCode}, errmsg={ErrMsg}",
                session.ErrCode, session.ErrMsg);
            return BadRequest(new { message = "微信登录失败，请重试" });
        }

        // 2. 查找已有用户
        var user = await _userManager.FindByLoginAsync("WeChat", session.OpenId);

        if (user == null)
        {
            // 3. 创建新用户
            user = new ApplicationUser($"wx_{session.OpenId}", null);
            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                _logger.LogError("微信用户创建失败: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return BadRequest(new { message = "用户创建失败" });
            }

            // 关联微信登录
            await _userManager.AddLoginAsync(user,
                new UserLoginInfo("WeChat", session.OpenId, "WeChat"));

            // 新用户默认赋予管理员角色
            var addRoleResult = await _userManager.AddToRoleAsync(user, "Admin");
            if (!addRoleResult.Succeeded)
            {
                _logger.LogWarning("微信用户默认角色分配失败: {Errors}",
                    string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }
        }

        // 4. 生成 JWT
        var token = await _tokenService.CreateToken(user);
        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new { token, userName = user.UserName, roles });
    }
}
