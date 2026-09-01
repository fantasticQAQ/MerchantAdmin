
namespace MerchantAdmin.API.Application.Commands
{
    public record PayOrderCommand(int OrderId) : IRequest<int>;
}
