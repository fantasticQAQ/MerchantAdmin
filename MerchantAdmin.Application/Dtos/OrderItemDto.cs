namespace MerchantAdmin.Application.Dtos;

public record OrderItemDto(int ProductId, string ProductName, decimal Price, decimal Quantity);
