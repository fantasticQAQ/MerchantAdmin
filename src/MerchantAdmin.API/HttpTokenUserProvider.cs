using System.Net;
using System.Net.Http;
using System.Text.Json;
using MerchantAdmin.Shared.Authentication;
using Microsoft.Extensions.Configuration;

namespace MerchantAdmin.API;

/// <summary>
/// 通过内部 HTTP 接口向 Identity 服务获取用户 SecurityStamp 与最新角色，
/// 不直接访问 Identity 数据库（两个服务数据库已拆分）。
/// </summary>
public class HttpTokenUserProvider : ITokenUserProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public HttpTokenUserProvider(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<TokenUserInfo?> GetAsync(long userId)
    {
        var client = _httpClientFactory.CreateClient("Identity");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/users/{userId}/security-info");
        request.Headers.Add("X-Internal-Key", _config["InternalApi:Key"] ?? string.Empty);

        using var response = await client.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var info = JsonSerializer.Deserialize<SecurityInfoDto>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return info is null ? null : new TokenUserInfo(info.SecurityStamp, info.Roles);
    }

    private sealed record SecurityInfoDto(string SecurityStamp, List<string> Roles);
}
