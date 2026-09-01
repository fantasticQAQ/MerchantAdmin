
namespace MerchantAdmin.API.Application.Commands
{
    public record DeleteProductCommand(int ProductId) : IRequest<bool>;
}
