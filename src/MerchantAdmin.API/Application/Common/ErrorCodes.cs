namespace MerchantAdmin.API.Application.Common;

/// <summary>
/// 统一的业务错误码。与 ApiResponse.Code 对应，用于前端和调用方判断错误类型。
/// </summary>
public static class ErrorCodes
{
    /// <summary>成功。</summary>
    public const int Success = 0;

    /// <summary>业务规则校验失败（领域异常）。</summary>
    public const int DomainError = 40000;

    /// <summary>输入数据校验失败。</summary>
    public const int ValidationError = 40002;

    /// <summary>资源不存在。</summary>
    public const int NotFound = 40400;

    /// <summary>未认证。</summary>
    public const int Unauthorized = 40100;

    /// <summary>无权限。</summary>
    public const int Forbidden = 40300;

    /// <summary>服务器内部错误（未预期异常）。</summary>
    public const int InternalError = 50000;
}
