using MerchantAdmin.API.Application.Services;
using MerchantAdmin.Domain.Events;

namespace MerchantAdmin.API.Application.DomainEventHandlers
{
    /// <summary>
    /// 取消/超时关闭订单时按需回补库存。
    /// 未发起支付：立刻回补；支付处理中：等支付失败事件再回补。
    /// </summary>
    public class OrderCancelledDomainEventHandler(OrderInventoryReturnService inventoryReturn)
        : INotificationHandler<OrderCancelledDomainEvent>, INotificationHandler<OrderTimedOutDomainEvent>
    {
        public Task Handle(OrderCancelledDomainEvent evt, CancellationToken ct)
            => RestoreIfNeeded(evt.RestoreInventory, evt.Order, ct);

        public Task Handle(OrderTimedOutDomainEvent evt, CancellationToken ct)
            => RestoreIfNeeded(evt.RestoreInventory, evt.Order, ct);

        private Task RestoreIfNeeded(bool restoreInventory, Order order, CancellationToken ct)
        {
            if (!restoreInventory)
                return Task.CompletedTask;

            return inventoryReturn.ReturnAsync(order, ct);
        }
    }
}
