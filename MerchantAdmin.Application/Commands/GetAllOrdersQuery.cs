using MediatR;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Commands
{
    public record GetAllOrdersQuery : IRequest<List<OrderDto>>;
}