using MediatR;

namespace MerchantAdmin.Application.Commands
{
    public record DeleteOrderCommand(int OrderId) : IRequest<bool>;
}
