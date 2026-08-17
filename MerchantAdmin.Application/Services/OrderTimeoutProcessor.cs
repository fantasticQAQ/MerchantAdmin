using MerchantAdmin.Application.IntegrationEvents;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.EventBus.Events;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MerchantAdmin.Application.Services;

/// <summary>
/// 订单超时关闭处理器（过期事件与兜底扫描共用）：
/// 将订单标记为 TimedOut、回补库存，支付处理中订单通知支付网关终止在途支付。
/// </summary>
public class OrderTimeoutProcessor(AppDbContext db, IOrderingIntegrationEventService orderingIntegrationEventService, ILogger<OrderTimeoutProcessor> logger)
{
    /// <summary>按订单 ID 执行超时关闭（内部校验状态，非超时态订单静默跳过）。</summary>
    public async Task ProcessOrderAsync(long orderId, CancellationToken ct)
    {
        // 必须加载 OrderItems：超时关闭领域事件回补库存需要用到
        var order = await db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        // 终态订单跳过（已支付/已取消/已退款/已超时关闭）
        if (order is null || order.OrderStatus is OrderStatus.Paid or OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.TimedOut)
            return;

        // 支付处理中订单超时关闭：通知支付网关终止在途支付
        var wasPaymentProcessing = order.OrderStatus == OrderStatus.PaymentProcessing;

        // 超时关闭：独立终态 TimedOut（区别于用户主动取消 Cancelled）
        order.MarkAsTimedOut();

        if (wasPaymentProcessing)
        {
            await orderingIntegrationEventService.AddAndSaveEventAsync(new OrderPaymentCancelledIntegrationEvent(order.Id));
        }

        await db.SaveEntitiesAsync(ct);

        logger.LogInformation("订单 {OrderId} 超时关闭（{Source}）", order.Id, "timeout");
    }
}
