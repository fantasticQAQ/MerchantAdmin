using MediatR;

namespace MerchantAdmin.Application.Commands
{
    public record RefundOrderCommand(int OrderId) : IRequest<bool>;
}
