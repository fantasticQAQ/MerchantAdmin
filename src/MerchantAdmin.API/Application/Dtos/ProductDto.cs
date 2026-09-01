namespace MerchantAdmin.API.Application.Dtos
{
    public record ProductDto(int ProductId, string Name, decimal Price, decimal Stock, bool IsActive);
}
