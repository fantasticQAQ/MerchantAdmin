using FluentValidation;
using MerchantAdmin.Application.Commands;

namespace MerchantAdmin.Application.Validators;

/// <summary>创建商品的参数校验。</summary>
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.ProductDto).NotNull().WithMessage("商品信息不能为空");

        When(x => x.ProductDto is not null, () =>
        {
            RuleFor(x => x.ProductDto.Name)
                .NotEmpty().WithMessage("商品名称不能为空")
                .MaximumLength(100).WithMessage("商品名称不能超过 100 个字符");

            RuleFor(x => x.ProductDto.Price)
                .GreaterThan(0).WithMessage("商品价格必须大于 0");

            RuleFor(x => x.ProductDto.Stock)
                .GreaterThanOrEqualTo(0).WithMessage("商品库存不能为负数");
        });
    }
}
