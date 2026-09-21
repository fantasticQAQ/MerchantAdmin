using MerchantAdmin.Infrastructure.Idempotency;

namespace MerchantAdmin.API.Application.Commands
{
    /// <summary>
    /// 创建订单的幂等处理：重复请求不再重复下单、不再重复扣库存。
    /// 重复时返回 0 —— 请求 ID 与订单 Id 的对应关系没有落库，无法还原首次的订单 Id，
    /// 客户端应把 0 视为"已受理但无新订单"（前端只用它判断成功与否）。
    /// </summary>
    public class CreateOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CreateOrderCommand, int>> logger)
        : IdentifiedCommandHandler<CreateOrderCommand, int>(mediator, requestManager, logger)
    {
        protected override int CreateResultForDuplicateRequest() => 0;
    }

    /// <summary>取消订单的幂等处理：重复请求直接返回成功，不再触发"订单已不可取消"。</summary>
    public class CancelOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CancelOrderCommand, bool>> logger)
        : IdentifiedCommandHandler<CancelOrderCommand, bool>(mediator, requestManager, logger)
    {
        protected override bool CreateResultForDuplicateRequest() => true;
    }

    /// <summary>支付订单的幂等处理：重复请求不再重复发起支付、不再重复写发件箱事件。</summary>
    public class PayOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<PayOrderCommand, int>> logger)
        : IdentifiedCommandHandler<PayOrderCommand, int>(mediator, requestManager, logger)
    {
        // 同创建订单：失败返回路径拿不到首次的订单 Id，重复时返回 0
        protected override int CreateResultForDuplicateRequest() => 0;
    }

    /// <summary>退款订单的幂等处理：重复请求不再重复回补库存。</summary>
    public class RefundOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<RefundOrderCommand, bool>> logger)
        : IdentifiedCommandHandler<RefundOrderCommand, bool>(mediator, requestManager, logger)
    {
        protected override bool CreateResultForDuplicateRequest() => true;
    }
}
