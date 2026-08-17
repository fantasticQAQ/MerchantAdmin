using MerchantAdmin.API.Common;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MerchantAdmin.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard()
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

            var dto = new DashboardDto(
                productCount,
                orderCount,
                paidOrderCount,
                pendingOrderCount,
                totalSales);

            return Ok(ApiResponse<DashboardDto>.Ok(dto));
        }
    }

    public record DashboardDto(
        int ProductCount,
        int OrderCount,
        int PaidOrderCount,
        int PendingOrderCount,
        decimal TotalSales);
}
