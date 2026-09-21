namespace MerchantAdmin.Domain.Entities
{
    /// <summary>幂等记录：以客户端请求 ID 为主键，用于识别并跳过重复请求。</summary>
    public class ClientRequest
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Time { get; set; }
    }
}
