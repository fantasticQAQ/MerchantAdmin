
namespace MerchantAdmin.API.Application.Commands
{
    public record DeleteOrderCommand(int OrderId) : IRequest<bool>;
}
