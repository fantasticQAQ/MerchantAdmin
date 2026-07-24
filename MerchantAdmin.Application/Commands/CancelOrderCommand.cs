using MediatR;

namespace MerchantAdmin.Application.Commands
{
    public record CancelOrderCommand(int OrderId) : IRequest<bool>;
}