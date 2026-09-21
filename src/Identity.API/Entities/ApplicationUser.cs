namespace Identity.API.Entities
{
    public class ApplicationUser : IdentityUser<long>
    {
        public ApplicationUser(string userName, string? email) : base(userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("用户名不能为空", nameof(userName));

            UserName = userName;
            Email = email;
        }

        /// <summary>
        /// 凭证版本号：改密码、重置密码、改角色、删账号时自增，并同步写入 Redis。
        /// 资源服务拿 token 里固化的 <c>ver</c> 与 Redis 中的当前值比对，不一致即拒绝，
        /// 所以自增就是"立刻撤销该用户所有已签发的 token"。
        /// </summary>
        public long TokenVersion { get; set; }
    }
}
