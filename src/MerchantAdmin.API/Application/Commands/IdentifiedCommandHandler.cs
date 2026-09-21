using MerchantAdmin.Infrastructure.Idempotency;

namespace MerchantAdmin.API.Application.Commands
{
    /// <summary>
    /// 幂等命令处理器基类：请求 ID 已处理过则直接返回 <see cref="CreateResultForDuplicateRequest"/>，
    /// 不再执行业务；否则登记请求 ID 后执行内层业务命令。
    /// </summary>
    /// <typeparam name="T">内层业务命令</typeparam>
    /// <typeparam name="R">内层业务命令的返回类型</typeparam>
    public abstract class IdentifiedCommandHandler<T, R>(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<T, R>> logger)
        : IRequestHandler<IdentifiedCommand<T, R>, R>
        where T : IRequest<R>
    {
        /// <summary>检测到重复请求时返回的结果。</summary>
        protected abstract R CreateResultForDuplicateRequest();

        public async Task<R> Handle(IdentifiedCommand<T, R> message, CancellationToken cancellationToken)
        {
            // 登记记录只挂到变更跟踪器，由业务命令自己的 SaveEntitiesAsync 提交，
            // 与业务写入同属外层 TransactionBehavior 开启的事务。
            // 登记返回 false 说明该 ID 已存在（重复请求）。
            if (!await requestManager.CreateRequestForCommandAsync<T>(message.Id, cancellationToken))
            {
                logger.LogInformation("请求 {RequestId} 已处理过，跳过重复执行", message.Id);
                return CreateResultForDuplicateRequest();
            }

            // 这里不加 try/catch（eShop 是 catch { return default; }）：
            // 异常向上抛，让 TransactionBehavior 回滚事务、并且不登记请求 ID，
            // 否则会出现"请求已登记但业务没执行"的静默失败。
            return await mediator.Send(message.Command, cancellationToken);
        }
    }
}
