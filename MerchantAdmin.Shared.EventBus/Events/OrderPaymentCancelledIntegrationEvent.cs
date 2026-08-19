namespace MerchantAdmin.Shared.EventBus.Events;

/// <summary>支付取消事件：订单服务在取消支付处理中订单时发出，支付服务订阅后终止对应支付流程。</summary>
public record OrderPaymentCancelledIntegrationEvent : IntegrationEvent
{
    public int OrderId { get; }

    public OrderPaymentCancelledIntegrationEvent(int orderId) => OrderId = orderId;
}
