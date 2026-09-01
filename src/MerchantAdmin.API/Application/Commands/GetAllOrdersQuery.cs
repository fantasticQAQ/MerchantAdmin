using MerchantAdmin.API.Application.Dtos;

namespace MerchantAdmin.API.Application.Commands
{
    public record GetAllOrdersQuery(int? OrderId = null, string? Status = null, int Page = 1, int PageSize = 10) : IRequest<PagedResult<OrderDto>>;
}
