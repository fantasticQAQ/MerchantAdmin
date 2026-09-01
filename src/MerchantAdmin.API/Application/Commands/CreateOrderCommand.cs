using MerchantAdmin.API.Application.Dtos;

namespace MerchantAdmin.API.Application.Commands
{
    public record CreateOrderCommand(
        List<OrderItemDto> OrderItems
    ) : IRequest<int>;
}
