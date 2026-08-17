using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Payment.API.Controllers;

/// <summary>支付回调请求体（模拟第三方支付平台的通知格式）。</summary>
public record PaymentCallbackRequest(int OrderId, string Status, decimal Amount);

/// <summary>
/// 支付回调入口：模拟第三方支付平台的异步通知，演示回调验签。
/// 真实场景中，第三方支付成功后会用其私钥对通知内容签名，我们这里用 HMAC-SHA256 模拟。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentCallbackController : ControllerBase
{
    // 模拟第三方支付平台的回调密钥（真实场景中由第三方提供）
    private const string CallbackSecret = "mock-payment-callback-secret";

    [HttpPost("notify")]
    public IActionResult Notify(
        [FromBody] PaymentCallbackRequest request,
        [FromHeader(Name = "X-Signature")] string? signature)
    {
        // 验签：校验回调确实来自第三方，防止伪造回调
        var expected = ComputeSignature(request);
        if (signature != expected)
        {
            return BadRequest(new { message = "签名校验失败" });
        }

        // 验签通过后，根据 Status 处理支付结果（此处简化为直接返回成功）
        return Ok(new { message = "success" });
    }

    private static string ComputeSignature(PaymentCallbackRequest request)
    {
        var raw = $"{request.OrderId}-{request.Status}-{request.Amount}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(CallbackSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
