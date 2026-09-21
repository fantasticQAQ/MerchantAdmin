namespace Identity.API.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// 签发 access token。有效期很短（默认 15 分钟），资源服务只做签名校验，
        /// 因此里面固化的角色最迟会在下次刷新时被替换。
        /// </summary>
        Task<string> CreateAccessTokenAsync(ApplicationUser user);
    }
}
