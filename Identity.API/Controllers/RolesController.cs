using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class RolesController : ControllerBase
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RolesController(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // 角色列表（含已停用角色，前端显示状态并支持启用）
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Id)
                .ToListAsync();

            var result = new List<RoleDto>();
            foreach (var role in roles)
            {
                var userCount = (await _userManager.GetUsersInRoleAsync(role.Name!)).Count;
                result.Add(new RoleDto(role.Name!, userCount, role.IsActive));
            }

            return Ok(result);
        }

        // 新建角色：同名角色若已停用则重新启用
        [HttpPost]
        public async Task<IActionResult> Create(CreateRoleRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return BadRequest("角色名不能为空");
            }

            var existing = await _roleManager.FindByNameAsync(req.Name);
            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    // 之前被停用，重新启用
                    existing.IsActive = true;
                    await _roleManager.UpdateAsync(existing);
                    return Ok("角色已重新启用");
                }
                return BadRequest("角色已存在");
            }

            var result = await _roleManager.CreateAsync(new ApplicationRole(req.Name) { IsActive = true });
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok("创建成功");
        }

        // 停用角色（软删除）：内置角色不可停用
        [HttpDelete("{name}")]
        public async Task<IActionResult> Delete(string name)
        {
            // 内置角色保护
            if (name is "Admin" or "SuperAdmin" or "Operator")
            {
                return BadRequest("内置角色不可停用");
            }

            var role = await _roleManager.FindByNameAsync(name);
            if (role is null || !role.IsActive)
            {
                return NotFound("角色不存在");
            }

            role.IsActive = false;
            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok("角色已停用");
        }

        // 启用角色：停用的角色重新启用
        [HttpPost("{name}/activate")]
        public async Task<IActionResult> Activate(string name)
        {
            var role = await _roleManager.FindByNameAsync(name);
            if (role is null)
            {
                return NotFound("角色不存在");
            }

            if (role.IsActive)
            {
                return BadRequest("角色已是启用状态");
            }

            role.IsActive = true;
            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok("角色已启用");
        }

        // 硬删除角色：内置角色不可删，有用户的角色不可删，删除后不可恢复
        [HttpDelete("{name}/hard")]
        public async Task<IActionResult> HardDelete(string name)
        {
            // 内置角色保护
            if (name is "Admin" or "SuperAdmin" or "Operator")
            {
                return BadRequest("内置角色不可删除");
            }

            var role = await _roleManager.FindByNameAsync(name);
            if (role is null)
            {
                return NotFound("角色不存在");
            }

            // 有用户的角色不可硬删
            var users = await _userManager.GetUsersInRoleAsync(name);
            if (users.Any())
            {
                return BadRequest($"该角色下存在 {users.Count} 个用户，无法删除");
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok("角色已删除");
        }
    }

    public record RoleDto(string Name, int UserCount, bool IsActive);

    public record CreateRoleRequest(string Name);
}
