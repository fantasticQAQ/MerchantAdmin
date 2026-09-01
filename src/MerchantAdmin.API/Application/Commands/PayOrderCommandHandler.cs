using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.API.Application.IntegrationEvents;
using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.API.Application.Commands
{
    public class PayOrderCommandHandler(AppDbContext db, IDelayJobService delayJob, IOrderingIntegrationEventService orderingIntegrationEventService) : IRequestHandler<PayOrderCommand, int>
    {
        public async Task<int> Handle(PayOrderCommand cmd, CancellationToken ct)
        {
            var order = await db.Orders.FindAsync(cmd.OrderId, ct)
                         ?? throw new DomainException("订单不存在");

            // 领域状态机：待支付 → 支付处理中（重复支付/已支付/已取消都会被拦截）
            order.MarkAsPaymentProcessing();

            // 将"发起支付"事件写入发件箱（SaveEventAsync 内部 SaveChanges，
            // 订单状态变更与事件日志在同一事务中持久化，保证不丢消息）
            await orderingIntegrationEventService.AddAndSaveEventAsync(new OrderPaymentStartedIntegrationEvent(order.Id));

            // 取消超时自动取消任务（支付已受理，不再因超时取消）
            await delayJob.CancelCancelOrderAsync(order.Id);

            // 订单状态最终由支付服务的支付成功回调驱动回写为已支付
            return order.Id;
        }
    }
}
