using MediatR;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Commands
{
    public record CreateProductCmommand(ProductDto ProductDto) : IRequest<int>;
}