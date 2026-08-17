using MediatR;

namespace MerchantAdmin.Application.Commands
{
    public record DeleteProductCommand(int ProductId) : IRequest<bool>;
}
