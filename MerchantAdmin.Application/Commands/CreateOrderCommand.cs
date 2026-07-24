using MediatR;
using MerchantAdmin.Application.Dtos;

public record CreateOrderCommand(
    List<OrderItemDto> OrderItems
) : IRequest<int>;