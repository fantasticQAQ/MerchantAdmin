using MerchantAdmin.Shared.EventBus.RabbitMQ;
using MerchantAdmin.Shared.IntegrationEventLog.Services;
using MerchantAdmin.API.Middlewares;
using MerchantAdmin.API;
using MerchantAdmin.Application;
using MerchantAdmin.Application.Services;
using MerchantAdmin.Shared.Authentication;
using Microsoft.OpenApi.Models;
using Serilog;
using MerchantAdmin.API.Application.Commands;
using MerchantAdmin.API.Application.Behaviors;
using MerchantAdmin.API.Application.IntegrationEvents;
using MerchantAdmin.API.Application.IntegrationEvents.EventHandling;
using MerchantAdmin.API.Infrastructure.Caching;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// ===== 添加 CORS（限制允许的域名，生产按配置收紧）=====
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
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

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CancelOrderCommand).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidatorBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
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
builder.Services.AddSingleton<IRedisConnectionProvider, RedisConnectionProvider>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<IDelayJobService, RedisDelayJobService>();

// 组合缓存框架：分布式锁 + 布隆过滤器 + 延时双删
builder.Services.AddSingleton<IDistributedLock, RedisDistributedLock>();
builder.Services.AddSingleton<IBloomFilter, RedisBloomFilter>();
builder.Services.AddSingleton<ICacheAsideService, CacheAsideService>();

//// 注册策略
//services.AddAuthorization(options =>
//{
//    options.AddPolicy("AtLeast18", policy =>
//        policy.Requirements.Add(new MinimumAgeRequirement(18)));
//});
//// 使用
//[Authorize(Policy = "AtLeast18")]

// 1. 控制器 + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
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
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"));
});


// 4. JWT 认证（统一抽到共享库：签名校验 + SecurityStamp 校验 + 实时角色刷新）
builder.Services.AddAppJwtAuthentication(builder.Configuration);
// 通过内部 HTTP 接口向 Identity 服务获取用户安全信息（两个服务数据库已拆分）
builder.Services.AddHttpClient("Identity", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["InternalApi:BaseUrl"]!);
});
builder.Services.AddScoped<ITokenUserProvider, HttpTokenUserProvider>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health").AllowAnonymous();

//多个 Pod 同时启动时可能并发迁移（SQL Server 会锁表，一般不会炸，但会报错）所以放部署文件中
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    db.Database.Migrate();
//}

// 7. Swagger（仅开发环境启用，生产关闭避免泄露接口）
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
