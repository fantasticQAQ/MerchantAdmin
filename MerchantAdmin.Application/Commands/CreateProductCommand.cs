using MediatR;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Commands
{
    public record CreateProductCommand(ProductDto ProductDto) : IRequest<int>;
}
