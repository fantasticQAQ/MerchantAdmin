using MerchantAdmin.Infrastructure.Caching;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAdmin.Api.Controllers
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
        public async Task<IActionResult> SetRedisKey()
        {
            await _cache.SetAsync("testKey", "testValue");
            return Ok();
        }

        [HttpGet("getRedisKey")]
        public async Task<IActionResult> GetRedisKey()
        {
            var value = await _cache.GetAsync<string>("testKey");
            return Ok(value);
        }

        [HttpPost("log")]
        public async Task<IActionResult> Log(string message)
        {
            _logger.LogInformation("Log message: {Message}", message);
            return Ok();
        }
    }
}