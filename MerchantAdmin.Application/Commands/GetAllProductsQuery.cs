using MediatR;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Commands
{
    public record GetAllProductsQuery(string? Name = null, int Page = 1, int PageSize = 10) : IRequest<PagedResult<ProductDto>>;
}
