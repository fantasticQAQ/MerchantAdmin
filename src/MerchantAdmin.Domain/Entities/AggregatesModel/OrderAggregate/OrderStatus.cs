using System.Text.Json.Serialization;

namespace MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Created = 1,
    Paid = 2,
    Cancelled = 3,
    PaymentProcessing = 4,
    Refunded = 5,
    /// <summary>超时关闭：待支付/支付处理中订单超时未完成支付，由系统自动关闭（区别于用户主动取消）。</summary>
    TimedOut = 6,
}
