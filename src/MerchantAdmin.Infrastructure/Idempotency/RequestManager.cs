using MerchantAdmin.Domain.Entities;

namespace MerchantAdmin.Infrastructure.Idempotency;

public class RequestManager(AppDbContext context) : IRequestManager
{
    public async Task<bool> ExistAsync(Guid id, CancellationToken ct = default)
        => await context.FindAsync<ClientRequest>(new object[] { id }, ct) is not null;

    /// <summary>
    /// 只 Add 到变更跟踪器，不调用 SaveChanges：
    /// 由业务方（UpdateProductCommandHandler）的 SaveEntitiesAsync 与扣减库存一起提交，
    /// 避免"标记已处理但业务失败"导致重试被误判为重复。
    /// </summary>
    public async Task<bool> CreateRequestForCommandAsync<T>(Guid id, CancellationToken ct = default)
    {
        if (await ExistAsync(id, ct))
        {
            return false;
        }

        context.Add(new ClientRequest
        {
            Id = id,
            Name = typeof(T).Name,
            Time = DateTime.UtcNow
        });

        return true;
    }
}
