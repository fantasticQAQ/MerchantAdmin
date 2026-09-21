namespace MerchantAdmin.Shared.EventBus.Events;

/// <summary>支付失败或在途支付被终止：订单服务据此把仍预占的库存退回货架。</summary>
public record OrderPaymentFailedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }
    public string Reason { get; }

    public OrderPaymentFailedIntegrationEvent(int orderId, string reason)
    {
        OrderId = orderId;
        Reason = reason;
    }
}
