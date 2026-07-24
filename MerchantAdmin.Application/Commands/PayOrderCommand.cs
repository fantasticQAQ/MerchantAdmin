using MediatR;

namespace MerchantAdmin.Application
{
    public record PayOrderCommand(int OrderId) : IRequest<int>;
}