using MerchantAdmin.API.Application.Commands;

namespace MerchantAdmin.API.Application.Validators;

/// <summary>取消订单：OrderId 必须为正整数。</summary>
public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("订单 Id 无效");
    }
}

/// <summary>支付订单：OrderId 必须为正整数。</summary>
public class PayOrderCommandValidator : AbstractValidator<PayOrderCommand>
{
    public PayOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("订单 Id 无效");
    }
}

/// <summary>删除商品：ProductId 必须为正整数。</summary>
public class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("商品 Id 无效");
    }
}

/// <summary>删除订单：OrderId 必须为正整数。</summary>
public class DeleteOrderCommandValidator : AbstractValidator<DeleteOrderCommand>
{
    public DeleteOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("订单 Id 无效");
    }
}

/// <summary>更新商品：ProductId 必须为正整数。</summary>
public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("商品 Id 无效");
    }
}

/// <summary>退款订单：OrderId 必须为正整数。</summary>
public class RefundOrderCommandValidator : AbstractValidator<RefundOrderCommand>
{
    public RefundOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("订单 Id 无效");
    }
}
