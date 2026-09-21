using MerchantAdmin.API.Application.Services;

namespace MerchantAdmin.API.Application.Commands
{
    public class RefundOrderCommandHandler(AppDbContext db, OrderInventoryReturnService inventoryReturn)
        : IRequestHandler<RefundOrderCommand, bool>
    {
        public async Task<bool> Handle(RefundOrderCommand request, CancellationToken ct)
        {
            var order = await db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);

            if (order is null)
            {
                return false;
            }

            // 领域状态机：仅已支付订单可退款（Paid → Refunded）
            order.MarkAsRefunded();

            await inventoryReturn.ReturnAsync(order, ct);

            await db.SaveEntitiesAsync(ct);

            return true;
        }
    }
}
