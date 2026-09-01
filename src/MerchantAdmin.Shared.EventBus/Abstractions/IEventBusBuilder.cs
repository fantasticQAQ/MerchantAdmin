using Microsoft.Extensions.DependencyInjection;

namespace MerchantAdmin.Shared.EventBus.Abstractions;

public interface IEventBusBuilder
{
    public IServiceCollection Services { get; }
}
