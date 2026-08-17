using MediatR;
using MerchantAdmin.API.Common;
using MerchantAdmin.Application.Commands;
using MerchantAdmin.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAdmin.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ProductsController(IMediator mediator)
            => _mediator = mediator;

        // 写操作仅限 Admin / Operator
        [HttpPost]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<int>>> Create(CreateProductCommand cmd)
        {
            var productId = await _mediator.Send(cmd);
            return Ok(ApiResponse<int>.Ok(productId));
        }

        [HttpPut("{productId}")]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> Update(int productId, UpdateProductCommand cmd)
        {
            var result = await _mediator.Send(cmd with { ProductId = productId });
            return Ok(ApiResponse<bool>.Ok(result));
        }

        [HttpDelete("{productId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int productId)
        {
            var result = await _mediator.Send(new DeleteProductCommand(productId));
            return Ok(ApiResponse<bool>.Ok(result));
        }

        // 查询所有登录用户可用
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetProducts(
            [FromQuery] string? name, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var products = await _mediator.Send(new GetAllProductsQuery(name, page, pageSize));
            return Ok(ApiResponse<PagedResult<ProductDto>>.Ok(products));
        }
    }
}
