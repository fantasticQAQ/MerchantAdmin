using System.Text;
using MerchantAdmin.API.Application.Commands;
using MerchantAdmin.API.Application.Common;
using MerchantAdmin.API.Application.Dtos;

namespace MerchantAdmin.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly AppDbContext _db;

        public OrdersController(IMediator mediator, AppDbContext db)
        {
            _mediator = mediator;
            _db = db;
        }

        /// <summary>
        /// 创建订单。请求头 <c>x-requestid</c> 必填：客户端为每次下单生成一个 GUID，
        /// 超时重发时带同一个 GUID，服务端只下一次单（不会重复扣库存）。
        /// </summary>
        [HttpPost("create")]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<int>>> Create(
            CreateOrderCommand cmd,
            [FromHeader(Name = "x-requestid")] Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                return BadRequest(ApiResponse<int>.Fail(400, "请求头 x-requestid 不能为空 GUID"));
            }

            var orderId = await _mediator.Send(new IdentifiedCommand<CreateOrderCommand, int>(cmd, requestId));
            return Ok(ApiResponse<int>.Ok(orderId));
        }

        [HttpPost("{orderId}/cancel")]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> Cancel(
            int orderId,
            [FromHeader(Name = "x-requestid")] Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                return BadRequest(ApiResponse<bool>.Fail(400, "请求头 x-requestid 不能为空 GUID"));
            }

            var result = await _mediator.Send(
                new IdentifiedCommand<CancelOrderCommand, bool>(new CancelOrderCommand(orderId), requestId));
            return Ok(ApiResponse<bool>.Ok(result));
        }

        [HttpPost("{orderId}/pay")]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<int>>> Pay(
            int orderId,
            [FromHeader(Name = "x-requestid")] Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                return BadRequest(ApiResponse<int>.Fail(400, "请求头 x-requestid 不能为空 GUID"));
            }

            var result = await _mediator.Send(
                new IdentifiedCommand<PayOrderCommand, int>(new PayOrderCommand(orderId), requestId));
            return Ok(ApiResponse<int>.Ok(result));
        }

        [HttpPost("{orderId}/refund")]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> Refund(
            int orderId,
            [FromHeader(Name = "x-requestid")] Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                return BadRequest(ApiResponse<bool>.Fail(400, "请求头 x-requestid 不能为空 GUID"));
            }

            var result = await _mediator.Send(
                new IdentifiedCommand<RefundOrderCommand, bool>(new RefundOrderCommand(orderId), requestId));
            return Ok(ApiResponse<bool>.Ok(result));
        }

        /// <summary>删除订单。软删 + IsDeleted 判断，重复调用天然无副作用，故不加幂等键。</summary>
        [HttpDelete("{orderId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int orderId)
        {
            var result = await _mediator.Send(new DeleteOrderCommand(orderId));
            return Ok(ApiResponse<bool>.Ok(result));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetOrders(
            [FromQuery] int? orderId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var orders = await _mediator.Send(new GetAllOrdersQuery(orderId, status, page, pageSize));
            return Ok(ApiResponse<PagedResult<OrderDto>>.Ok(orders));
        }

        // 导出订单 CSV（Admin/Operator 可用）
        [HttpGet("export")]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<IActionResult> Export([FromQuery] string? status)
        {
            var query = _db.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            {
                query = query.Where(o => o.OrderStatus == orderStatus);
            }

            var orders = await query.OrderBy(o => o.Id).ToListAsync();

            var productNames = await _db.Products
                .AsNoTracking()
                .ToDictionaryAsync(p => p.Id, p => p.Name);

            var sb = new StringBuilder();
            sb.AppendLine("订单ID,状态,创建时间,商品明细,总金额");

            foreach (var o in orders)
            {
                var detail = string.Join(";", o.OrderItems.Select(i =>
                    $"{(productNames.TryGetValue(i.ProductId, out var n) ? n : "已删除商品")} x{i.Quantity}"));
                var total = o.OrderItems.Sum(i => i.Price * i.Quantity);

                sb.AppendLine($"{o.Id},{o.OrderStatus},{o.CreatedAt:yyyy-MM-dd HH:mm:ss},{detail},{total}");
            }

            // 带 BOM 的 UTF-8，Excel 打开中文不乱码
            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            return File(bytes, "text/csv; charset=utf-8", $"orders_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }
    }
}
