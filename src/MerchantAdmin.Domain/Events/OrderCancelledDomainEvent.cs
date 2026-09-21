using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

namespace MerchantAdmin.Domain.Events
{
    public record OrderCancelledDomainEvent(Order Order, bool RestoreInventory) : INotification;
}
