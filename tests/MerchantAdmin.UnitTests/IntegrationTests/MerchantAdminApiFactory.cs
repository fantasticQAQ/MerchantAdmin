using MerchantAdmin.Shared.EventBus.Abstractions;
using MerchantAdmin.Shared.IntegrationEventLog.Services;
using MerchantAdmin.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using MerchantAdmin.API.Application.IntegrationEvents;
using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.UnitTests.IntegrationTests;

/// <summary>
/// 集成测试工厂：启动 MerchantAdmin.API 的测试服务器，
/// 将数据库替换为 SQLite 内存库（支持事务），并 mock 掉 Redis / RabbitMQ 等外部依赖。
/// </summary>
public sealed class MerchantAdminApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public MerchantAdminApiFactory()
    {
        // SQLite 内存库需要保持连接打开，否则数据库随连接关闭而销毁
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 1. 移除所有后台服务（Redis 过期订单消费器、RabbitMQ 事件总线）
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hosted in hostedServices)
            {
                services.Remove(hosted);
            }

            // 2. 移除 RabbitMQ 连接与事件总线（避免连接真实 RabbitMQ）
            services.RemoveAll<IConnection>();
            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(_ => new Mock<IEventBus>().Object);

            // 3. 数据库替换为 SQLite 内存库（支持事务，TransactionBehavior 需要）
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            // 4. Redis 相关替换为内存实现 / mock
            services.RemoveAll<IRedisConnectionProvider>();
            services.RemoveAll<ICacheService>();
            services.AddScoped<ICacheService, InMemoryCacheService>();

            services.RemoveAll<IDelayJobService>();
            services.AddScoped(_ => new Mock<IDelayJobService>().Object);

            // 5. 集成事件日志服务替换为 mock
            services.RemoveAll<IIntegrationEventLogService>();
            services.AddScoped(_ => new Mock<IIntegrationEventLogService>().Object);

            services.RemoveAll<IOrderingIntegrationEventService>();
            services.AddScoped(_ => new Mock<IOrderingIntegrationEventService>().Object);
        });
    }

    /// <summary>确保 SQLite 表结构已创建（在首次请求前调用）。</summary>
    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
