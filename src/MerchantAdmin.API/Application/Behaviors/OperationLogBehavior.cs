using System.Security.Claims;
using System.Text.Json;
using MerchantAdmin.Domain.Entities;

namespace MerchantAdmin.API.Application.Behaviors
{
    /// <summary>
    /// 操作日志管道：自动记录所有写操作（Command），查询（Query）不记录。
    /// 日志写入失败不影响主流程。
    /// </summary>
    public class OperationLogBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<OperationLogBehavior<TRequest, TResponse>> _logger;

        public OperationLogBehavior(
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor,
            ILogger<OperationLogBehavior<TRequest, TResponse>> logger)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var response = await next();

            // 只记录写操作（Command），跳过查询
            if (typeof(TRequest).Name.EndsWith("Query"))
            {
                return response;
            }

            try
            {
                var userName = _httpContextAccessor.HttpContext?.User
                    ?.FindFirstValue(ClaimTypes.Name) ?? "system";

                _db.OperationLogs.Add(new OperationLog
                {
                    UserName = userName,
                    Action = typeof(TRequest).Name,
                    Detail = JsonSerializer.Serialize(request),
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // 日志写入失败不影响主流程
                _logger.LogWarning(ex, "写入操作日志失败");
            }

            return response;
        }
    }
}
