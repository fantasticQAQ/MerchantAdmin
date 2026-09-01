using Payment.API.Payments;

namespace Payment.API.EventHandling;

/// <summary>订阅"发起支付"事件：调用支付渠道，成功后发布"支付成功"事件。</summary>
public class OrderPaymentStartedIntegrationEventHandler(
    IPaymentProvider paymentProvider,
    IEventBus eventBus,
    ILogger<OrderPaymentStartedIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderPaymentStartedIntegrationEvent>
{
    public async Task Handle(OrderPaymentStartedIntegrationEvent @event)
    {
        logger.LogInformation("收到发起支付事件，订单 {OrderId} 开始支付", @event.OrderId);

        // 调用支付渠道（模拟第三方支付）
        var result = await paymentProvider.PayAsync(new PaymentRequest(@event.OrderId, 0m));

        if (result.Success)
        {
            // 支付成功 → 发布支付成功事件（订单服务订阅后回写状态为已支付）
            await eventBus.PublishAsync(new OrderPaymentSucceededIntegrationEvent(@event.OrderId));
            logger.LogInformation("订单 {OrderId} 支付成功，已发布支付成功事件", @event.OrderId);
        }
        else
        {
            logger.LogWarning("订单 {OrderId} 支付失败：{Reason}", @event.OrderId, result.FailureReason);
        }
    }
}
