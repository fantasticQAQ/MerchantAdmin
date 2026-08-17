using EventBus.Extensions;
using EventBusRabbitMQ;
using MerchantAdmin.EventBus.Events;
using Payment.API.EventHandling;
using Payment.API.Payments;

var builder = WebApplication.CreateBuilder(args);

// RabbitMQ 事件总线 + 订阅"发起支付"/"取消支付"事件
builder.AddRabbitMqEventBus()
    .AddSubscription<OrderPaymentStartedIntegrationEvent, OrderPaymentStartedIntegrationEventHandler>()
    .AddSubscription<OrderPaymentCancelledIntegrationEvent, OrderPaymentCancelledIntegrationEventHandler>();

// 支付渠道：现在用模拟实现，未来接支付宝/微信只需替换为对应 Provider
builder.Services.AddSingleton<IPaymentProvider, MockPaymentProvider>();

// 支付会话存储：记录在途支付是否被取消（内存实现，模拟支付网关的支付单状态）
builder.Services.AddSingleton<IPaymentSessionStore, InMemoryPaymentSessionStore>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program { }
