namespace Identity.API.Services
{
    /// <summary>
    /// refresh token 的签发、校验与作废。access token 缩短到分钟级之后，
    /// "这个用户还行不行" 的判断全部集中到这里，资源服务不再承担任何在线校验。
    /// </summary>
    public interface IRefreshTokenService
    {
        /// <summary>为用户签发一个新的 refresh token，返回明文（仅此一次可见，库里只存哈希）。</summary>
        Task<string> IssueAsync(ApplicationUser user, CancellationToken ct = default);

        /// <summary>
        /// 校验并轮换。成功返回对应用户，失败返回 null。
        /// 校验内容包括：token 是否存在、是否已作废、是否过期、用户是否还在、SecurityStamp 是否变过。
        /// </summary>
        Task<ApplicationUser?> ConsumeAsync(string refreshToken, CancellationToken ct = default);

        /// <summary>作废某用户全部未作废的 refresh token。</summary>
        Task RevokeAllAsync(long userId, CancellationToken ct = default);
    }
}
