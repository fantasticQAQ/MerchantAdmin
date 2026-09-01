using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MerchantAdmin.UnitTests.IntegrationTests;

public class ProductsApiIntegrationTests : IClassFixture<MerchantAdminApiFactory>
{
    private readonly HttpClient _client;

    // 与 appsettings.json 中的 Jwt 配置保持一致
    private const string JwtKey = "SuperSecretKey_ThisShouldBeLonger_ForSecurity12345";
    private const string JwtIssuer = "http://localhost:5034";

    public ProductsApiIntegrationTests(MerchantAdminApiFactory factory)
    {
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // ===== 认证 =====

    [Fact]
    public async Task 未携带Token访问商品接口_应返回401()
    {
        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ===== 商品 CRUD =====

    [Fact]
    public async Task 创建商品_应返回商品Id()
    {
        Authorize();

        var body = new
        {
            productDto = new { productId = 0, name = "iPhone", price = 6999m, stock = 10m }
        };

        var response = await _client.PostAsJsonAsync("/api/products", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        result!.Code.Should().Be(0);
        result.Data.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task 创建商品_名称为空_应返回400校验失败()
    {
        Authorize();

        var body = new
        {
            productDto = new { productId = 0, name = "", price = 6999m, stock = 10m }
        };

        var response = await _client.PostAsJsonAsync("/api/products", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Code.Should().Be(40002);
    }

    [Fact]
    public async Task 查询商品列表_应返回已创建的商品()
    {
        Authorize();

        // 先创建一个商品
        var createBody = new
        {
            productDto = new { productId = 0, name = "MacBook", price = 9999m, stock = 5m }
        };
        await _client.PostAsJsonAsync("/api/products", createBody);

        // 再查询
        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductDto>>>();
        result!.Code.Should().Be(0);
        result.Data.Items.Should().Contain(p => p.Name == "MacBook");
    }

    [Fact]
    public async Task 删除商品_应返回成功()
    {
        Authorize();

        var createBody = new
        {
            productDto = new { productId = 0, name = "iPad", price = 4999m, stock = 3m }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/products", createBody);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<int>>();

        var response = await _client.DeleteAsync($"/api/products/{created!.Data}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        result!.Code.Should().Be(0);
        result.Data.Should().BeTrue();
    }

    // ===== 辅助方法 =====

    /// <summary>生成一个有效 JWT 并附加到请求头。</summary>
    private void Authorize()
    {
        var token = GenerateJwt();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static string GenerateJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "1"),
                new Claim(JwtRegisteredClaimNames.UniqueName, "testuser"),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ===== 响应模型 =====

    private record ApiResponse<T>(int Code, string Message, T Data, bool Success);

    private record PagedResult<T>(int Total, int Page, int PageSize, List<T> Items);

    private record ProductDto(int ProductId, string Name, decimal Price, decimal Stock, bool IsActive);
}
