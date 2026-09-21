using MerchantAdmin.API.Application.Commands;

namespace MerchantAdmin.API.Application.Validators;

/// <summary>幂等包装命令：请求 ID 必须是有效 GUID（对应请求头 x-requestid）。</summary>
public class IdentifiedCommandValidator : AbstractValidator<IdentifiedCommand<UpdateProductCommand, bool>>
{
    public IdentifiedCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("请求头 x-requestid 无效");
    }
}
