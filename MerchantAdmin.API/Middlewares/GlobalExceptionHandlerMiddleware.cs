using System.Net;
using System.Text.Json;
using FluentValidation;
using MerchantAdmin.API.Common;
using MerchantAdmin.Domain.Exceptions;

namespace MerchantAdmin.API.Middlewares;

/// <summary>
/// 全局异常处理中间件：捕获未处理异常，转换为统一的 ApiResponse 错误结构。
/// 放置位置应尽量靠前（在 UseRouting 之后、其他中间件之前），以覆盖后续管道的所有异常。
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = MapException(exception);

        // 提取真正的校验异常（可能是 DomainException 内嵌的 ValidationException）
        var validationException = exception as ValidationException
            ?? (exception as DomainException)?.InnerException as ValidationException;

        // 校验异常单独拆出明细，便于前端定位字段错误
        ApiResponse<object?>? response = validationException is not null
            ? BuildValidationResponse(validationException)
            : ApiResponse.Fail(ToBizCode(statusCode), message);

        if (statusCode >= HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "未处理异常: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("业务/校验异常: {Message}", exception.Message);
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    /// <summary>将异常映射为 HTTP 状态码 + 对外提示。</summary>
    private static (HttpStatusCode StatusCode, string Message) MapException(Exception exception)
    {
        switch (exception)
        {
            case DomainException domainException when domainException.InnerException is ValidationException validation:
                return (HttpStatusCode.BadRequest, validation.Message);

            case DomainException domainException:
                return (HttpStatusCode.BadRequest, domainException.Message);

            case ValidationException validationException:
                return (HttpStatusCode.BadRequest, validationException.Message);

            case UnauthorizedAccessException:
                return (HttpStatusCode.Unauthorized, "未授权访问");

            default:
                // 生产环境不暴露内部异常细节
                return (HttpStatusCode.InternalServerError, "服务器内部错误，请稍后重试");
        }
    }

    private static ApiResponse<object?> BuildValidationResponse(ValidationException validationException)
    {
        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return new ApiResponse<object?>
        {
            Code = ErrorCodes.ValidationError,
            Message = "参数校验失败",
            Data = errors
        };
    }

    /// <summary>由 HTTP 状态码推导业务错误码。</summary>
    private static int ToBizCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => ErrorCodes.DomainError,
            HttpStatusCode.Unauthorized => ErrorCodes.Unauthorized,
            HttpStatusCode.Forbidden => ErrorCodes.Forbidden,
            HttpStatusCode.NotFound => ErrorCodes.NotFound,
            _ => ErrorCodes.InternalError
        };
    }
}
