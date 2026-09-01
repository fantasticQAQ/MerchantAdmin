using MerchantAdmin.API.Application.Common;
using MerchantAdmin.API.Infrastructure.Caching;

namespace MerchantAdmin.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ICacheService _cache;
        private readonly ILogger<TestController> _logger;
        public TestController(ICacheService cache, ILogger<TestController> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        [HttpPost("setRedisKey")]
        public async Task<ActionResult<ApiResponse>> SetRedisKey()
        {
            await _cache.SetAsync("testKey", "testValue");
            return Ok(ApiResponse.Ok());
        }

        [HttpGet("getRedisKey")]
        public async Task<ActionResult<ApiResponse<string>>> GetRedisKey()
        {
            var value = await _cache.GetAsync<string>("testKey");
            return Ok(ApiResponse<string>.Ok(value!));
        }

        [HttpPost("log")]
        public ActionResult<ApiResponse> Log(string message)
        {
            _logger.LogInformation("Log message: {Message}", message);
            return Ok(ApiResponse.Ok());
        }
    }
}
