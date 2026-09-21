using MerchantAdmin.Shared.Authentication;

namespace Identity.API.Services
{
    /// <summary>
    /// 凭证撤销：把用户的凭证版本号 +1 并写入 Redis。
    /// 写入成功后，该用户所有已签发的 access token 会在下一个请求上被拒绝
    /// （资源服务比对 token 里的 ver 与 Redis 中的当前值）。
    /// </summary>
    public interface ICredentialRevoker
    {
        /// <summary>撤销该用户的所有 token（改密码、重置密码、改角色时调用）。</summary>
        Task RevokeAsync(ApplicationUser user, CancellationToken ct = default);

        /// <summary>
        /// 账号已被删除后调用。库里已经没有这行，无法再自增，
        /// 只能按删除前的版本号 +1 直接写进 Redis，让在途 token 立即失效。
        /// </summary>
        Task RevokeForDeletedUserAsync(long userId, long versionBeforeDelete, CancellationToken ct = default);
    }

    public class CredentialRevoker : ICredentialRevoker
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenVersionStore _versionStore;
        private readonly ILogger<CredentialRevoker> _logger;

        public CredentialRevoker(
            UserManager<ApplicationUser> userManager,
            ITokenVersionStore versionStore,
            ILogger<CredentialRevoker> logger)
        {
            _userManager = userManager;
            _versionStore = versionStore;
            _logger = logger;
        }

        public async Task RevokeAsync(ApplicationUser user, CancellationToken ct = default)
        {
            user.TokenVersion++;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                // 库里没存下新版本就不能写 Redis：否则 Redis 会比数据库"新"，
                // 用户重新登录拿到的仍是旧版本号，会陷入"登录即失效"的死循环。
                _logger.LogError("自增凭证版本号失败，本次撤销未生效：{Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            await _versionStore.SetAsync(user.Id, user.TokenVersion, ct);
        }

        public Task RevokeForDeletedUserAsync(long userId, long versionBeforeDelete, CancellationToken ct = default) =>
            _versionStore.SetAsync(userId, versionBeforeDelete + 1, ct);
    }
}
