using MerchantAdmin.Shared.Authentication;

namespace Identity.API.Services;

/// <summary>
/// 通过 Identity 的 UserManager 读取 SecurityStamp 与最新角色。
/// </summary>
public class UserManagerTokenUserProvider : ITokenUserProvider
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserManagerTokenUserProvider(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<TokenUserInfo?> GetAsync(long userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new TokenUserInfo(user.SecurityStamp ?? string.Empty, roles.ToList());
    }
}
