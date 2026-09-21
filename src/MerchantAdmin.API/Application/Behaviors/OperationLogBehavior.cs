using System.Security.Claims;
using System.Text.Json;
using MerchantAdmin.API.Application.Commands;
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

            // IdentifiedCommand 只是幂等包装、不是真实业务命令：
            // 日志由内层命令自己的管道记录，这里跳过，避免一次操作写两条。
            if (typeof(TRequest).IsGenericType &&
                typeof(TRequest).GetGenericTypeDefinition() == typeof(IdentifiedCommand<,>))
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
