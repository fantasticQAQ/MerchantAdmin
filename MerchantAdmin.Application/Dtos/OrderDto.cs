using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

namespace MerchantAdmin.Application.Dtos
{
    public record OrderDto(int OrderId, DateTime CreatedAt, OrderStatus OrderStatus, IReadOnlyList<OrderItemDto> OrderItems);
}
