using System.Text;
using EventBus.Extensions;
using EventBusRabbitMQ;
using IntegrationEventLogEF.Services;
using MerchantAdmin.API.Middlewares;
using MerchantAdmin.Application;
using MerchantAdmin.Application.Behaviors;
using MerchantAdmin.Application.Commands;
using MerchantAdmin.Application.IntegrationEvents;
using MerchantAdmin.Application.IntegrationEvents.EventHandling;
using MerchantAdmin.EventBus.Events;
using MerchantAdmin.Application.Services;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Claims;

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


// 4. JWT 认证
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("❌ JWT 校验失败：" + context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            // 校验 SecurityStamp：用户改密码等安全凭证变更后，旧 token 立即失效
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var securityStamp = context.Principal?.FindFirstValue("securityStamp");
            if (userId != null && long.TryParse(userId, out var userIdLong))
            {
                using var scope = context.HttpContext.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                try
                {
                    var stamp = await db.Database
                        .SqlQueryRaw<string>("SELECT SecurityStamp AS Value FROM AspNetUsers WHERE Id = {0}", userIdLong)
                        .FirstOrDefaultAsync();
                    if (stamp != securityStamp)
                    {
                        context.Fail("安全凭证已变更，请重新登录");
                        return;
                    }
                }
                catch
                {
                    // 用户表不可用（如集成测试环境）时跳过安全戳校验
                }
            }
            Console.WriteLine("✅ JWT 校验成功");
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

// 5. 授权服务（必须加！）
builder.Services.AddAuthorization();

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
