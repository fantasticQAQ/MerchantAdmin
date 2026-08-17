using FluentValidation;
using MerchantAdmin.Application.Commands;
using MerchantAdmin.Application.Dtos;

namespace MerchantAdmin.Application.Validators;

/// <summary>创建订单的参数校验。</summary>
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.OrderItems)
            .NotNull().WithMessage("订单项不能为空")
            .NotEmpty().WithMessage("订单至少包含一个商品");

        RuleForEach(x => x.OrderItems)
            .SetValidator(new OrderItemDtoValidator());
    }
}

/// <summary>订单项校验。</summary>
public class OrderItemDtoValidator : AbstractValidator<OrderItemDto>
{
    public OrderItemDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0).WithMessage("商品 Id 无效");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("购买数量必须大于 0");
    }
}
