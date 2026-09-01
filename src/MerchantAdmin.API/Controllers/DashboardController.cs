using MerchantAdmin.API.Application.Common;
using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICacheAsideService _cache;

        public DashboardController(AppDbContext db, ICacheAsideService cache)
        {
            _db = db;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard(CancellationToken ct)
        {
            // 统计数字实时性要求低（延迟 30 秒无感知），缓存避免每次全表扫描
            var dto = await _cache.GetOrAddAsync(
                "dashboard:stats",
                BuildDashboardAsync,
                TimeSpan.FromSeconds(30),
                ct);

            return Ok(ApiResponse<DashboardDto>.Ok(dto!));
        }

        private async Task<DashboardDto> BuildDashboardAsync()
        {
            var productCount = await _db.Products.CountAsync();
            var orderCount = await _db.Orders.CountAsync(o => !o.IsDeleted);
            var paidOrderCount = await _db.Orders.CountAsync(o => !o.IsDeleted && o.OrderStatus == OrderStatus.Paid);
            var pendingOrderCount = await _db.Orders.CountAsync(
                o => !o.IsDeleted && (o.OrderStatus == OrderStatus.Created || o.OrderStatus == OrderStatus.PaymentProcessing));

            // 销售额 = 已支付订单的订单项金额之和
            var paidOrders = await _db.Orders
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.OrderStatus == OrderStatus.Paid)
                .Include(o => o.OrderItems)
                .ToListAsync();

            var totalSales = paidOrders.Sum(o => o.OrderItems.Sum(i => i.Price * i.Quantity));

            return new DashboardDto(
                productCount,
                orderCount,
                paidOrderCount,
                pendingOrderCount,
                totalSales);
        }
    }

    public record DashboardDto(
        int ProductCount,
        int OrderCount,
        int PaidOrderCount,
        int PendingOrderCount,
        decimal TotalSales);
}
