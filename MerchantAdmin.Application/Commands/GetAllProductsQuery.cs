using MediatR;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Commands
{
    public record GetAllProductsQuery : IRequest<List<ProductDto>>;
}