
namespace MerchantAdmin.API.Application.Commands
{
    public record CancelOrderCommand(int OrderId) : IRequest<bool>;
}
