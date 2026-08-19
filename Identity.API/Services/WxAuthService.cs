using System.Text.Json.Serialization;

namespace Identity.API.Services;

/// <summary>
/// 微信 code2session 接口返回结果
/// </summary>
public class WxSessionResult
{
    [JsonPropertyName("openid")]
    public string OpenId { get; set; } = string.Empty;

    [JsonPropertyName("session_key")]
    public string SessionKey { get; set; } = string.Empty;

    [JsonPropertyName("unionid")]
    public string? UnionId { get; set; }

    [JsonPropertyName("errcode")]
    public int? ErrCode { get; set; }

    [JsonPropertyName("errmsg")]
    public string? ErrMsg { get; set; }

    public bool IsSuccess => ErrCode == null || ErrCode == 0;
}

/// <summary>
/// 微信小程序认证服务
/// </summary>
public class WxAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public WxAuthService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    /// <summary>
    /// 用临时 code 换取 openid 和 session_key
    /// </summary>
    public async Task<WxSessionResult> Code2SessionAsync(string code)
    {
        var appId = _config["WeChat:AppId"]!;
        var secret = _config["WeChat:AppSecret"]!;

        var url = $"https://api.weixin.qq.com/sns/jscode2session" +
                  $"?appid={appId}" +
                  $"&secret={secret}" +
                  $"&js_code={code}" +
                  $"&grant_type=authorization_code";

        var result = await _httpClient.GetFromJsonAsync<WxSessionResult>(url);
        return result ?? new WxSessionResult { ErrCode = -1, ErrMsg = "微信接口返回为空" };
    }
}