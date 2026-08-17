namespace MerchantAdmin.API.Common;

/// <summary>
/// 统一的 API 响应格式，所有接口返回值都通过它包装。
/// </summary>
/// <typeparam name="T">业务数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>业务状态码，0 表示成功，非 0 表示业务失败。</summary>
    public int Code { get; set; }

    /// <summary>对外的提示信息。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>业务数据，失败时为 default。</summary>
    public T? Data { get; set; }

    /// <summary>是否成功。</summary>
    public bool Success => Code == 0;

    public static ApiResponse<T> Ok(T data, string message = "success")
        => new() { Code = 0, Message = message, Data = data };

    public static ApiResponse<T> Fail(int code, string message)
        => new() { Code = code, Message = message, Data = default };
}

/// <summary>无数据载荷的统一响应。</summary>
public class ApiResponse : ApiResponse<object?>
{
    public static ApiResponse<object?> Ok(string message = "success")
        => new() { Code = 0, Message = message, Data = null };

    public static new ApiResponse<object?> Fail(int code, string message)
        => new() { Code = code, Message = message, Data = null };
}
