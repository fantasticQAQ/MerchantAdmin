using MediatR;
using MerchantAdmin.Application.IntegrationEvents;
using MerchantAdmin.Application.IntegrationEvents.Events;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;

namespace MerchantAdmin.Application.Commands
{
    public class PayOrderCommandHandler(AppDbContext db, IDelayJobService delayJob, IOrderingIntegrationEventService orderingIntegrationEventService) : IRequestHandler<PayOrderCommand, int>
    {
        public async Task<int> Handle(PayOrderCommand cmd, CancellationToken ct)
        {
            var order = await db.Orders.FindAsync(cmd.OrderId, ct)
                         ?? throw new DomainException("订单不存在");

            await orderingIntegrationEventService.AddAndSaveEventAsync(new OrderPaymentSucceededIntegrationEvent(order.Id));

            // 3️⃣ 订单标记为已支付
            order.MarkAsPaid();

            // 5️⃣ 保存
            await db.SaveEntitiesAsync(ct);

            // 4️⃣ 取消延迟任务
            await delayJob.CancelCancelOrderAsync(order.Id);
            return order.Id;
        }
    }
}
