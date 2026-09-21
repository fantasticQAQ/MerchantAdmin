namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICredentialRevoker _revoker;

        public UsersController(UserManager<ApplicationUser> userManager, ICredentialRevoker revoker)
        {
            _userManager = userManager;
            _revoker = revoker;
        }

        // 用户列表（含角色）
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userManager.Users.OrderBy(u => u.Id).ToListAsync();

            var result = new List<UserDto>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new UserDto(user.Id, user.UserName!, user.Email, roles.ToList()));
            }

            return Ok(result);
        }

        // 新增用户（可分配多个角色）
        [HttpPost]
        public async Task<IActionResult> Create(CreateUserRequest req)
        {
            var user = new ApplicationUser(req.UserName, req.Email);

            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            // 超级管理员角色不可通过普通接口分配（仅系统初始化）
            if (req.Roles?.Contains("SuperAdmin") == true)
            {
                return BadRequest("超级管理员角色不可分配");
            }

            if (req.Roles is { Count: > 0 })
            {
                var addRoles = await _userManager.AddToRolesAsync(user, req.Roles);
                if (!addRoles.Succeeded)
                {
                    return BadRequest(addRoles.Errors);
                }
            }

            return Ok("创建成功");
        }

        // 编辑用户（邮箱、多角色）
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, UpdateUserRequest req)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return NotFound("用户不存在");
            }

            // 邮箱可选：null/空视为无邮箱，允许编辑时清空
            if (req.Email != user.Email)
            {
                user.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email;
                await _userManager.UpdateAsync(user);
            }

            // 更新角色：整体替换为用户勾选的角色列表
            if (req.Roles is not null)
            {
                // 内置超级管理员（demo）的角色完全不可编辑：不能加、不能减、不能改
                if (user.UserName == "admin")
                {
                    return BadRequest("内置超级管理员的角色不可修改");
                }

                var currentRoles = await _userManager.GetRolesAsync(user);
                var rolesChanged = !currentRoles.OrderBy(r => r).SequenceEqual(req.Roles.OrderBy(r => r));

                // 超级管理员角色不可新分配给普通用户（已是超管的可保留）
                if (req.Roles.Contains("SuperAdmin") && !currentRoles.Contains("SuperAdmin"))
                {
                    return BadRequest("超级管理员角色不可分配");
                }

                // 移除核心角色（Admin 或 SuperAdmin）时的保护
                var removingCoreRole =
                    (currentRoles.Contains("Admin") && !req.Roles.Contains("Admin"))
                    || (currentRoles.Contains("SuperAdmin") && !req.Roles.Contains("SuperAdmin"));

                if (removingCoreRole)
                {
                    // 最后管理员保护：不能移除最后一个管理员的 Admin 角色
                    if (currentRoles.Contains("Admin") && !req.Roles.Contains("Admin"))
                    {
                        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                        if (adminUsers.Count <= 1)
                        {
                            return BadRequest("不能移除最后一个管理员的管理员角色");
                        }
                    }
                }

                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRolesAsync(user, req.Roles);

                // 角色真的变了才撤销：客户端拿到 401 后会自动续期，续期时重新读库拿到新角色，
                // 表现为"无感换权"而不是被踢下线。只是改了邮箱时不动版本号，避免无谓地把人踢一遍。
                if (rolesChanged)
                {
                    await _revoker.RevokeAsync(user);
                }
            }

            return Ok("更新成功");
        }

        // 删除用户
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return NotFound("用户不存在");
            }

            // 内置超级管理员保护
            if (user.UserName == "admin")
            {
                return BadRequest("内置超级管理员不可删除");
            }

            // 防锁死：不能删除最后一个管理员
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains("Admin"))
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                if (adminUsers.Count <= 1)
                {
                    return BadRequest("不能删除最后一个管理员");
                }
            }

            // 删完之后库里没有这一行了，无法再自增版本号，所以先记下当前值，
            // 删除成功后按它 +1 写进 Redis，让这个账号在途的 token 立即失效。
            var versionBeforeDelete = user.TokenVersion;

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            await _revoker.RevokeForDeletedUserAsync(id, versionBeforeDelete);

            return Ok("删除成功");
        }

        // 管理员重置用户密码（内置超级管理员密码不可重置）
        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest req)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return NotFound("用户不存在");
            }

            // 内置超级管理员保护：其密码不可由其他管理员重置
            if (user.UserName == "admin")
            {
                return BadRequest("内置超级管理员的密码不可重置");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, req.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            // 重置密码等同于改密码：撤销该用户所有已签发的 token，让在途会话立即失效
            await _revoker.RevokeAsync(user);

            return Ok("密码已重置");
        }
    }

    public record UserDto(long Id, string UserName, string? Email, List<string> Roles);

    public record CreateUserRequest(string UserName, string? Email, string Password, List<string>? Roles);

    public record UpdateUserRequest(string? Email, List<string>? Roles);

    public record ResetPasswordRequest(string NewPassword);
}
