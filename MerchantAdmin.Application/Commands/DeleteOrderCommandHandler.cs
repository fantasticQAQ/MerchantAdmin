using MediatR;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MerchantAdmin.Application.Commands
{
    public class DeleteOrderCommandHandler(AppDbContext db) : IRequestHandler<DeleteOrderCommand, bool>
    {
        public async Task<bool> Handle(DeleteOrderCommand request, CancellationToken ct)
        {
            var order = await db.Orders.FindAsync(request.OrderId, ct);

            if (order is null || order.IsDeleted)
            {
                return false;
            }

            // 业务规则：仅终态/交易完成的订单可删除（取消、已支付、已退款、超时关闭）。
            // 待支付/支付处理中不可删除 —— 支付可能在途，删除后支付成功回调会导致
            // 订单状态与库存不一致。此类订单应通过「取消」操作结束生命周期，而非删除。
            if (order.OrderStatus is OrderStatus.Created or OrderStatus.PaymentProcessing)
            {
                throw new DomainException("订单支付处理中，不可删除；请先取消订单或等待支付结果");
            }

            // 删除 = 纯归档：仅标记软删除，不改变库存。
            // 库存只由交易状态变更驱动（下单扣减，取消/超时关闭/退款回补），
            // 删除订单不产生也不消除库存变化，避免与交易流程重复/冲突。
            order.MarkAsDeleted();
            await db.SaveEntitiesAsync(ct);

            return true;
        }
    }
}
