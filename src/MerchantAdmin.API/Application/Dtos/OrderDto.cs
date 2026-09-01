
namespace MerchantAdmin.API.Application.Dtos
{
    public record OrderDto(int OrderId, DateTime CreatedAt, OrderStatus OrderStatus, IReadOnlyList<OrderItemDto> OrderItems);
}
