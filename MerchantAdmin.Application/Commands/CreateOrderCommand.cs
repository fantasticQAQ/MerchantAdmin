using MediatR;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Commands
{
    public record CreateOrderCommand(
        List<OrderItemDto> OrderItems
    ) : IRequest<int>;
}
