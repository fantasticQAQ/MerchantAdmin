namespace Identity.API.Entities
{
    /// <summary>
    /// refresh token 记录（用于把 access token 的有效期压到分钟级后仍能保持登录态）。
    /// 只保存明文的 SHA-256 哈希：即便数据库泄露，也无法直接拿去换 token。
    /// </summary>
    public class RefreshToken
    {
        public long Id { get; set; }

        public long UserId { get; set; }

        /// <summary>token 明文的 SHA-256 十六进制串。</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// 签发时用户的 SecurityStamp。校验时与库中现值比对，不一致说明改过密码，
        /// 该 refresh token 立即失效——这是「撤销检查前移到刷新时」的落点。
        /// </summary>
        public string SecurityStamp { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        /// <summary>作废时间；null 表示仍有效。轮换、重用检测都会写入。</summary>
        public DateTime? RevokedAt { get; set; }
    }
}
