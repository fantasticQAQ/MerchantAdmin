using MerchantAdmin.API.Application.Dtos;

namespace MerchantAdmin.API.Application.Commands
{
    public record CreateProductCommand(ProductDto ProductDto) : IRequest<int>;
}
