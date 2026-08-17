namespace Payment.API.Payments;

/// <summary>模拟支付渠道：延迟后返回成功，模拟第三方支付的异步处理。</summary>
public sealed class MockPaymentProvider(ILogger<MockPaymentProvider> logger, IPaymentSessionStore sessionStore) : IPaymentProvider
{
    public async Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct = default)
    {
        // 登记支付会话，便于"取消支付"事件终止在途支付
        sessionStore.Start(request.OrderId);

        // 模拟第三方支付渠道的处理耗时
        await Task.Delay(500, ct);

        // 支付处理期间订单被取消（订单服务发来取消支付事件）→ 放弃本次支付
        if (sessionStore.IsCancelled(request.OrderId))
        {
            logger.LogWarning("订单 {OrderId} 支付期间已被取消，本次支付终止", request.OrderId);
            sessionStore.Remove(request.OrderId);
            return new PaymentResult(false, "订单已取消，支付终止");
        }

        logger.LogInformation("模拟支付渠道处理订单 {OrderId}，金额 {Amount}", request.OrderId, request.Amount);

        sessionStore.Remove(request.OrderId);
        return new PaymentResult(true);
    }
}
