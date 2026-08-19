using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

/// <summary>
/// 供内部服务（Merchant）调用的用户安全信息接口。
/// 不走 JWT，改用共享内部密钥（X-Internal-Key）认证，仅在内网暴露。
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public InternalUsersController(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config = config;
    }

    [HttpGet("users/{id:long}/security-info")]
    public async Task<IActionResult> GetSecurityInfo(long id)
    {
        var key = Request.Headers["X-Internal-Key"].FirstOrDefault();
        var expected = _config["InternalApi:Key"];
        if (string.IsNullOrEmpty(expected) || key != expected)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new { securityStamp = user.SecurityStamp ?? string.Empty, roles });
    }
}
