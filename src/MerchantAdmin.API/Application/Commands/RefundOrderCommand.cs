
namespace MerchantAdmin.API.Application.Commands
{
    public record RefundOrderCommand(int OrderId) : IRequest<bool>;
}
