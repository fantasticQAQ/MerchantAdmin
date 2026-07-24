using MediatR;

namespace MerchantAdmin.Application.Commands
{
    public record DeleteProductCmommand(int ProductId):IRequest<bool>;
}