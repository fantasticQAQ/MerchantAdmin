using MerchantAdmin.API.Application.IntegrationEvents;
using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.API.Application.Commands
{
    public class CancelOrderCommandHandler(AppDbContext db, IDelayJobService delayJob, IOrderingIntegrationEventService orderingIntegrationEventService)
            : IRequestHandler<CancelOrderCommand, bool>
    {
        public async Task<bool> Handle(CancelOrderCommand command, CancellationToken ct)
        {
            // 必须加载 OrderItems：取消时领域事件回补库存需要用到
            var order = await db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

            if (order == null)
            {
                return false;
            }

            // 记录取消前的状态：若订单正处于"支付处理中"，必须通知支付网关终止支付，
            // 否则支付成功回调仍会把订单写回已支付（取消与回调并发时会互相覆盖）
            var wasPaymentProcessing = order.OrderStatus == OrderStatus.PaymentProcessing;

            order.Cancel();

            if (wasPaymentProcessing)
            {
                // 取消支付事件与订单状态变更在同一事务（outbox），保证不丢
                await orderingIntegrationEventService.AddAndSaveEventAsync(new OrderPaymentCancelledIntegrationEvent(order.Id));
            }

            await delayJob.CancelCancelOrderAsync(order.Id);

            return await db.SaveEntitiesAsync(ct);
        }
    }
}
