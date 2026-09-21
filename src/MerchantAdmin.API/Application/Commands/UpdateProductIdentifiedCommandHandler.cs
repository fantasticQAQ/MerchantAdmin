using MerchantAdmin.Infrastructure.Idempotency;

namespace MerchantAdmin.API.Application.Commands
{
    /// <summary>更新商品（含库存扣减）的幂等处理：重复请求直接返回成功，不再重复扣减。</summary>
    public class UpdateProductIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<UpdateProductCommand, bool>> logger)
        : IdentifiedCommandHandler<UpdateProductCommand, bool>(mediator, requestManager, logger)
    {
        protected override bool CreateResultForDuplicateRequest() => true;
    }
}
