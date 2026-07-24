using MediatR;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.Extensions.Logging;

namespace MerchantAdmin.Application.Commands
{
    public class CancelOrderCommandHandler(AppDbContext db, IDelayJobService delayJob) : IRequestHandler<CancelOrderCommand, bool>
    {
        public async Task<bool> Handle(CancelOrderCommand command, CancellationToken ct)
        {
            var order = await db.Orders.FindAsync(command.OrderId, ct);
            if (order == null)
            {
                return false;
            }
            order.Cancel();

            // 4️⃣ 取消延迟任务
            await delayJob.CancelCancelOrderAsync(order.Id);

            return await db.SaveEntitiesAsync(ct);
        }
    }
}
