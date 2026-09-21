using MerchantAdmin.API.Application.Commands;
using MerchantAdmin.API.Application.Common;
using MerchantAdmin.API.Application.Dtos;
using MerchantAdmin.API.Infrastructure.Caching;
using IDatabase = StackExchange.Redis.IDatabase;

namespace MerchantAdmin.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICacheAsideService _cache;
        private readonly IDatabase _redis;

        // 商品列表缓存：版本号方案。key 带版本，商品写操作 INCR 版本 → 旧列表缓存自然作废
        private const string ProductVersionKey = "product:version";
        private static readonly TimeSpan ProductListTtl = TimeSpan.FromMinutes(10);

        public ProductsController(IMediator mediator, ICacheAsideService cache, IRedisConnectionProvider provider)
        {
            _mediator = mediator;
            _cache = cache;
            _redis = provider.Connection.GetDatabase();
        }

        // 写操作仅限 Admin / Operator
        [HttpPost]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<int>>> Create(CreateProductCommand cmd)
        {
            var productId = await _mediator.Send(cmd);
            await InvalidateProductListCacheAsync();
            return Ok(ApiResponse<int>.Ok(productId));
        }

        /// <summary>
        /// 更新商品（含库存扣减）。请求头 <c>x-requestid</c> 必填，作为幂等键：
        /// 客户端为每次操作生成一个 GUID，超时重发时带同一个 GUID，服务端只执行一次。
        /// </summary>
        [HttpPut("{productId}")]
        [Authorize(Roles = "Admin,Operator,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> Update(
            int productId,
            UpdateProductCommand cmd,
            [FromHeader(Name = "x-requestid")] Guid requestId)
        {
            if (requestId == Guid.Empty)
            {
                return BadRequest(ApiResponse<bool>.Fail(400, "请求头 x-requestid 不能为空 GUID"));
            }

            var identified = new IdentifiedCommand<UpdateProductCommand, bool>(
                cmd with { ProductId = productId }, requestId);

            var result = await _mediator.Send(identified);
            await InvalidateProductListCacheAsync();
            return Ok(ApiResponse<bool>.Ok(result));
        }

        [HttpDelete("{productId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int productId)
        {
            var result = await _mediator.Send(new DeleteProductCommand(productId));
            await InvalidateProductListCacheAsync();
            return Ok(ApiResponse<bool>.Ok(result));
        }

        // 查询所有登录用户可用
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetProducts(
            [FromQuery] string? name, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var version = await _redis.StringGetAsync(ProductVersionKey);
            var cacheKey = $"product:list:v{version}:{name ?? ""}:{page}:{pageSize}";

            var products = await _cache.GetOrAddAsync(
                cacheKey,
                () => _mediator.Send(new GetAllProductsQuery(name, page, pageSize), ct),
                ProductListTtl,
                ct);

            return Ok(ApiResponse<PagedResult<ProductDto>>.Ok(products!));
        }

        /// <summary>商品变更后版本号 +1，使所有商品列表缓存作废（旧 key 随 TTL 过期回收）。</summary>
        private async Task InvalidateProductListCacheAsync()
        {
            await _redis.StringIncrementAsync(ProductVersionKey);
        }
    }
}
