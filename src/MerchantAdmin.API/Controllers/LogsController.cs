using MerchantAdmin.API.Application.Common;
using MerchantAdmin.API.Application.Dtos;

namespace MerchantAdmin.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class LogsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public LogsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<LogDto>>>> GetLogs(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var total = await _db.OperationLogs.CountAsync();

            var logs = await _db.OperationLogs
                .AsNoTracking()
                .OrderByDescending(l => l.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = logs
                .Select(l => new LogDto(l.Id, l.UserName, l.Action, l.Detail, l.CreatedAt))
                .ToList();

            return Ok(ApiResponse<PagedResult<LogDto>>.Ok(new PagedResult<LogDto>(total, page, pageSize, items)));
        }
    }

    public record LogDto(long Id, string UserName, string Action, string Detail, DateTime CreatedAt);
}
