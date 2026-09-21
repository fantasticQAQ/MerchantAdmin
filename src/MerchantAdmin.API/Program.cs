using MerchantAdmin.API;
using MerchantAdmin.API.Application.Behaviors;
using MerchantAdmin.API.Application.Commands;
using MerchantAdmin.API.Application.IntegrationEvents;
using MerchantAdmin.API.Application.IntegrationEvents.EventHandling;
using MerchantAdmin.API.Application.Services;
using MerchantAdmin.API.Infrastructure.Caching;
using MerchantAdmin.API.Middlewares;
using MerchantAdmin.Application;
using MerchantAdmin.Application.Services;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Infrastructure.Idempotency;
using MerchantAdmin.Infrastructure.Services;
using MerchantAdmin.Shared.Authentication;
using MerchantAdmin.Shared.EventBus.RabbitMQ;
using MerchantAdmin.Shared.IntegrationEventLog.Services;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// ===== 添加 CORS（限制允许的域名，生产按配置收紧）=====
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration);
});

services.AddScoped<IProductStockService, ProductStockService>();
services.AddScoped<IProductListCacheInvalidator, RedisProductListCacheInvalidator>();
// 幂等请求管理器（基于 ClientRequests 表，与业务写入同事务）
services.AddScoped<IRequestManager, RequestManager>();
// 库存回补服务（退货/取消订单把预占库存退回货架），具体类需显式注册
services.AddScoped<OrderInventoryReturnService>();

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CancelOrderCommand).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidatorBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
    // OperationLogBehavior 排在 TransactionBehavior 之后（内层）：日志与业务写入同属一个事务。
    // 注意：只把它调到前面并不能让日志落到事务外——扣库存走 IdentifiedCommand<T>（幂等包装）→
    // 内层 T 的嵌套 Send，内层 TransactionBehavior 因 HasActiveTransaction 直接穿透，真实事务由
    // 外层持有并在整个内层链返回后才 Commit。要把日志挪出事务得改层次，不是改注册顺序。
    cfg.AddOpenBehavior(typeof(OperationLogBehavior<,>));
});

// 注册应用层服务（含 FluentValidation 校验器）
services.AddApplication();

// 供操作日志获取当前用户
services.AddHttpContextAccessor();

services.AddHostedService<RedisExpiredOrderConsumer>();

// 3. 添加RabbitMq
builder.AddRabbitMqEventBus()
    .AddSubscription<OrderPaymentSucceededIntegrationEvent, OrderPaymentSucceededIntegrationEventHandler>();

services.AddTransient<IIntegrationEventLogService, IntegrationEventLogService<AppDbContext>>();
services.AddTransient<IOrderingIntegrationEventService, OrderingIntegrationEventService>();

// 2. 添加 Redis 
//builder.Services.AddStackExchangeRedisCache(options =>
//{
//    options.Configuration =
//        builder.Configuration["Redis:ConnectionString"];
//});


services.AddSingleton<IRedisConnectionProvider, RedisConnectionProvider>();
services.AddScoped<ICacheService, RedisCacheService>();
services.AddScoped<IDelayJobService, RedisDelayJobService>();

// 凭证版本号校验复用上面那条 Redis 连接，不额外建连接。
// token 里的 ver 与 Redis 中的当前值不一致 → 401，客户端续期后可无感换权（改角色）或被踢下线（改密码）。
services.AddSingleton<IConnectionMultiplexer>(sp => sp.GetRequiredService<IRedisConnectionProvider>().Connection);
services.AddRedisTokenVersionStore();

// 组合缓存框架：分布式锁 + 布隆过滤器 + 延时双删
services.AddSingleton<IDistributedLock, RedisDistributedLock>();
services.AddSingleton<IBloomFilter, RedisBloomFilter>();
services.AddSingleton<ICacheAsideService, CacheAsideService>();

//// 注册策略
//services.AddAuthorization(options =>
//{
//    options.AddPolicy("AtLeast18", policy =>
//        policy.Requirements.Add(new MinimumAgeRequirement(18)));
//});
//// 使用
//[Authorize(Policy = "AtLeast18")]

// 1. 控制器 + Swagger
services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "MerchantAdmin.Api", Version = "v3" });

    // ✅ 1. 定义 JWT 认证
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "请输入 JWT Token，格式：Bearer {token}"
    });

    // ✅ 2. 全局要求认证
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 2. 数据库上下文
services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"));
});


// 4. JWT 认证（共享库：只做签名与有效期校验）
// 用户安全信息与实时角色不在这里查询，判断集中在 Identity 的 /auth/refresh，
// 详见 JwtAuthenticationExtensions 的说明。
services.AddAppJwtAuthentication(builder.Configuration);

services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health").AllowAnonymous();

//多个 Pod 同时启动时可能并发迁移（SQL Server 会锁表，一般不会炸，但会报错）所以放部署文件中
//using (var scope = app.services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    db.Database.Migrate();
//}

// 7. Swagger（仅开发环境启用，生产关闭避免泄露接口）System.AggregateException:“Some services are not able to be constructed (Error while validating the service descriptor 'ServiceType: MediatR.IRequestHandler`2[MerchantAdmin.API.Application.Commands.RefundOrderCommand,System.Boolean] Lifetime: Transient ImplementationType: MerchantAdmin.API.Application.Commands.RefundOrderCommandHandler': Unable to resolve service for type 'MerchantAdmin.API.Application.Services.OrderInventoryReturnService' while attempting to activate 'MerchantAdmin.API.Application.Commands.RefundOrderCommandHandler'.) (Error while validating the service descriptor 'ServiceType: MediatR.INotificationHandler`1[MerchantAdmin.Domain.Events.OrderCancelledDomainEvent] Lifetime: Transient ImplementationType: MerchantAdmin.API.Application.DomainEventHandlers.OrderCancelledDomainEventHandler': Unable to resolve service for type 'MerchantAdmin.API.Application.Services.OrderInventoryReturnService' while attempting to activate 'MerchantAdmin.API.Application.DomainEventHandlers.OrderCancelledDomainEventHandler'.) (Error while validating the service descriptor 'ServiceType: MediatR.INotificationHandler`1[MerchantAdmin.Domain.Events.OrderTimedOutDomainEvent] Lifetime: Transient ImplementationType: MerchantAdmin.API.Application.DomainEventHandlers.OrderCancelledDomainEventHandler': Unable to resolve service for type 'MerchantAdmin.API.Application.Services.OrderInventoryReturnService' while attempting to activate 'MerchantAdmin.API.Application.DomainEventHandlers.OrderCancelledDomainEventHandler'.)”

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// 全局异常处理中间件（尽量靠前，覆盖后续管道异常）
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// CORS（限制允许的前端域名）
app.UseCors("AllowFrontend");

// 8. 认证 & 授权（顺序不能错）
app.UseAuthentication(); // ✅ 必须加！
app.UseAuthorization();

app.MapControllers();

app.Run();

// 供集成测试的 WebApplicationFactory 引用
public partial class Program { }
