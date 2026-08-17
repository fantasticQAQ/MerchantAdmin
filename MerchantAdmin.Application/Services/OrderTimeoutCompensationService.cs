using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MerchantAdmin.Application.Services;

/// <summary>
/// 超时关闭兜底扫描：Redis 过期事件可能因通知丢失/服务重启而遗漏，
/// 本服务定期扫描"待支付且超过支付时限"的订单补关，与过期事件形成双保险。
/// 扫描间隔与支付时限均可在配置中调整（OrderTimeout:ScanIntervalMinutes / OrderTimeout:PaymentTimeoutMinutes）。
/// </summary>
public sealed class OrderTimeoutCompensationService(
    IServiceProvider sp,
    IOptions<OrderTimeoutOptions> options,
    ILogger<OrderTimeoutCompensationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.ScanIntervalMinutes));
        logger.LogInformation("超时兜底扫描启动，间隔 {Interval} 分钟，支付时限 {Timeout} 分钟",
            options.Value.ScanIntervalMinutes, options.Value.PaymentTimeoutMinutes);

        // 启动先扫一次，随后按间隔周期扫描
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await ScanAsync(ct);
            }
            catch (Exception ex)
            {
                // 扫描失败不影响下一轮（避免单次异常终止后台服务）
                logger.LogError(ex, "超时兜底扫描执行失败");
            }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var cutoff = DateTime.Now.AddMinutes(-options.Value.PaymentTimeoutMinutes);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<OrderTimeoutProcessor>();

        // 扫描超时未支付且尚未关闭的订单（Created = 待支付）
        var expiredOrderIds = await db.Orders
            .Where(o => o.OrderStatus == OrderStatus.Created && !o.IsDeleted && o.CreatedAt < cutoff)
            .Select(o => o.Id)
            .ToListAsync(ct);

        if (expiredOrderIds.Count == 0)
            return;

        logger.LogWarning("兜底扫描发现 {Count} 个超时未关闭订单：{Ids}", expiredOrderIds.Count, string.Join(",", expiredOrderIds));

        foreach (var orderId in expiredOrderIds)
        {
            await processor.ProcessOrderAsync(orderId, ct);
        }
    }
}

/// <summary>超时关闭配置。</summary>
public class OrderTimeoutOptions
{
    public const string SectionName = "OrderTimeout";

    /// <summary>兜底扫描间隔（分钟）。</summary>
    public double ScanIntervalMinutes { get; set; } = 5;

    /// <summary>支付时限：订单创建后超过该时长未支付则自动关闭（分钟）。</summary>
    public int PaymentTimeoutMinutes { get; set; } = 15;
}
