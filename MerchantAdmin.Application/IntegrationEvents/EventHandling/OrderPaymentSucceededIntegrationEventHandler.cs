using EventBus.Abstractions;
using MerchantAdmin.Application.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;

namespace MerchantAdmin.Application.IntegrationEvents.EventHandling
{
    public class OrderPaymentSucceededIntegrationEventHandler(
    ILogger<OrderPaymentSucceededIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderPaymentSucceededIntegrationEvent>
    {
        public Task Handle(OrderPaymentSucceededIntegrationEvent @event)
        {
            logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);
            return Task.CompletedTask;
        }
    }
}
