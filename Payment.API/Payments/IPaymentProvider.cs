namespace Payment.API.Payments;

/// <summary>支付请求。</summary>
public record PaymentRequest(int OrderId, decimal Amount);

/// <summary>支付结果。</summary>
public record PaymentResult(bool Success, string? FailureReason = null);

/// <summary>
/// 支付渠道抽象（Provider/Adapter 模式）。
/// 现在用 MockPaymentProvider 模拟，未来接支付宝/微信只需新增实现，其余代码不变。
/// </summary>
public interface IPaymentProvider
{
    Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct = default);
}
