namespace MerchantAdmin.API.Application.Commands
{
    /// <summary>
    /// 幂等命令包装：把业务命令与客户端提供的请求 ID（请求头 x-requestid）绑在一起，
    /// 交给 IdentifiedCommandHandler 判断是否为重复请求。
    /// </summary>
    /// <typeparam name="T">内层业务命令</typeparam>
    /// <typeparam name="R">内层业务命令的返回类型</typeparam>
    public class IdentifiedCommand<T, R> : IRequest<R>
        where T : IRequest<R>
    {
        public IdentifiedCommand(T command, Guid id)
        {
            Command = command;
            Id = id;
        }

        public T Command { get; }

        public Guid Id { get; }
    }
}
