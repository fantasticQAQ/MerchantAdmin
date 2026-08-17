using MediatR;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Commands
{
    public record GetAllOrdersQuery(int? OrderId = null, string? Status = null, int Page = 1, int PageSize = 10) : IRequest<PagedResult<OrderDto>>;
}
