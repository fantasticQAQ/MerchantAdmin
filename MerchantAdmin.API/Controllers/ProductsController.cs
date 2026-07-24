using MediatR;
using MerchantAdmin.Application.Commands;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAdmin.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ProductsController(IMediator mediator)
            => _mediator = mediator;

        [HttpPost]
        public async Task<IActionResult> Create(CreateProductCmommand cmd)
        {
            var orderId = await _mediator.Send(cmd);
            return Ok(orderId);
        }

        [HttpDelete("{productId}")]
        public async Task<IActionResult> Delete(int productId)
        {
            var orderId = await _mediator.Send(new DeleteProductCmommand(productId));
            return Ok(orderId);
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _mediator.Send(new GetAllProductsQuery());
            return Ok(products);
        }
    }
}
