using Payment.API.Payments;

namespace Payment.API.EventHandling;

/// <summary>订阅"取消支付"事件：终止对应订单的在途支付，后续不再发布支付成功事件。</summary>
public class OrderPaymentCancelledIntegrationEventHandler(
    IPaymentSessionStore sessionStore,
    ILogger<OrderPaymentCancelledIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderPaymentCancelledIntegrationEvent>
{
    public Task Handle(OrderPaymentCancelledIntegrationEvent @event)
    {
        // 标记该订单支付已取消：正在模拟支付中的流程完成后检测到取消标记，将放弃发布支付成功事件
        sessionStore.MarkCancelled(@event.OrderId);
        logger.LogInformation("订单 {OrderId} 支付已取消，支付流程终止", @event.OrderId);
        return Task.CompletedTask;
    }
}
