namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PingController : ControllerBase
    {
        [HttpPost]
        [Authorize]
        public string Ping()
        {
            return "pong";
        }

        [HttpGet]
        public string Test()
        {
            return "test";
        }
    }
}
