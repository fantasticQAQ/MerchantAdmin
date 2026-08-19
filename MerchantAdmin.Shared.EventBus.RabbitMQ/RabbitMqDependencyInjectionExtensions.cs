using MerchantAdmin.Shared.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace MerchantAdmin.Shared.EventBus.RabbitMQ;

public static class RabbitMqDependencyInjectionExtensions
{
    private const string SectionName = "EventBus";

    public static IEventBusBuilder AddRabbitMqEventBus(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var rabbitCfg = builder.Configuration.GetSection("RabbitMQ");
        builder.Services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = rabbitCfg["HostName"]!,
                UserName = rabbitCfg["UserName"]!,
                Password = rabbitCfg["Password"]!,
                Port = int.Parse(rabbitCfg["Port"]!),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        builder.Services.Configure<EventBusOptions>(builder.Configuration.GetSection(SectionName));
        builder.Services.AddSingleton<IEventBus, RabbitMQEventBus>();
        builder.Services.AddSingleton<IHostedService>(sp => (RabbitMQEventBus)sp.GetRequiredService<IEventBus>());
        return new EventBusBuilder(builder.Services);
    }

    private class EventBusBuilder(IServiceCollection services) : IEventBusBuilder
    {
        public IServiceCollection Services => services;
    }
}
