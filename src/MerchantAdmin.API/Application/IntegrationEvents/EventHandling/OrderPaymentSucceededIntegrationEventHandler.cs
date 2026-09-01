using MerchantAdmin.Shared.EventBus.Abstractions;
using MerchantAdmin.Domain.Exceptions;

namespace MerchantAdmin.API.Application.IntegrationEvents.EventHandling
{
    public class OrderPaymentSucceededIntegrationEventHandler(
        AppDbContext db,
        ILogger<OrderPaymentSucceededIntegrationEventHandler> logger) :
        IIntegrationEventHandler<OrderPaymentSucceededIntegrationEvent>
    {
        public async Task Handle(OrderPaymentSucceededIntegrationEvent @event)
        {
            var order = await db.Orders.FindAsync(@event.OrderId, CancellationToken.None);
            if (order is null)
            {
                logger.LogWarning("支付事件对应的订单不存在: {OrderId}", @event.OrderId);
                return;
            }

            // 已软删除的订单（终态删除场景）忽略支付回调，避免状态与库存不一致
            if (order.IsDeleted)
            {
                logger.LogWarning("订单 {OrderId} 已删除，忽略支付成功回调", @event.OrderId);
                return;
            }

            try
            {
                // 幂等：只有 Created 状态的订单才能标记为已支付
                order.MarkAsPaid();
                await db.SaveChangesAsync(CancellationToken.None);

                logger.LogInformation("订单 {OrderId} 支付确认完成", @event.OrderId);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 乐观并发冲突：回调加载订单后，订单已被其他流程修改（取消/退款/删除）——
                // RowVersion 已变化，本次回调的写入被拒绝，订单保持新状态（如 Cancelled），不覆盖
                logger.LogWarning("订单 {OrderId} 支付回调因并发冲突被拒绝（订单状态已变化），忽略回调: {Message}", @event.OrderId, ex.Message);
            }
            catch (DomainException ex)
            {
                // 订单可能已被超时取消，或消息重复投递（已支付）—— 幂等跳过，交由补偿机制处理
                logger.LogWarning("订单 {OrderId} 无法标记为已支付: {Message}", @event.OrderId, ex.Message);
            }
        }
    }
}
