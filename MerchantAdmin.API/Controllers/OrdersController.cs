using MediatR;
using MerchantAdmin.Application;
using MerchantAdmin.Application.Commands;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAdmin.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OrdersController(IMediator mediator)
            => _mediator = mediator;

        [HttpPost("create")]
        public async Task<IActionResult> Create(CreateOrderCommand cmd)
        {
            var orderId = await _mediator.Send(cmd);
            return Ok(orderId);
        }

        [HttpPost("{orderId}/cancel")]
        public async Task<IActionResult> Cancel(int orderId)
        {
            var result = await _mediator.Send(new CancelOrderCommand(orderId));
            return Ok(result);
        }

        [HttpPost("{orderId}/pay")]
        public async Task<IActionResult> Pay(int orderId)
        {
            await _mediator.Send(new PayOrderCommand(orderId));
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _mediator.Send(new GetAllOrdersQuery());
            return Ok(orders);
        }
    }
}
