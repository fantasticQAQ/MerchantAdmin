namespace MerchantAdmin.EventBus.Events;

/// <summary>发起支付事件：订单服务发出，支付服务订阅。</summary>
public record OrderPaymentStartedIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }

    public OrderPaymentStartedIntegrationEvent(int orderId) => OrderId = orderId;
}
