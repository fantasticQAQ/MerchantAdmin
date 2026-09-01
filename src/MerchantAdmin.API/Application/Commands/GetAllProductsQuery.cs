using MerchantAdmin.API.Application.Dtos;

namespace MerchantAdmin.API.Application.Commands
{
    public record GetAllProductsQuery(string? Name = null, int Page = 1, int PageSize = 10) : IRequest<PagedResult<ProductDto>>;
}
