namespace MerchantAdmin.Shared.EventBus.Events;

/// <summary>支付成功事件：支付服务发出，订单服务订阅。</summary>
public record OrderPaymentSucceededIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }

    public OrderPaymentSucceededIntegrationEvent(int orderId) => OrderId = orderId;
}
