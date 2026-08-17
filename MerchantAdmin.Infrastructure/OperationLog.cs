namespace MerchantAdmin.Infrastructure
{
    /// <summary>操作日志记录。</summary>
    public class OperationLog
    {
        public long Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
