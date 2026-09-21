
namespace MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

public class Order : Entity, IAggregateRoot
{
    public DateTime CreatedAt { get; private set; }
    public OrderStatus OrderStatus { get; private set; }

    /// <summary>乐观并发令牌：防止支付回调等并发写入覆盖取消/退款等其他流程的修改。</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>软删除标记：删除后数据保留（可追溯），查询过滤掉。</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// 预占库存是否已退回货架。下单后为 false；取消未支付 / 支付失败 / 退款成功后为 true。
    /// 已支付未退款保持 false（库存已售出，不是退回）。
    /// </summary>
    public bool InventoryReturned { get; private set; }

    private readonly List<OrderItem> _orderItems;
    public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();

    public Order()
    {
        _orderItems = new List<OrderItem>();
        OrderStatus = OrderStatus.Created;
        CreatedAt = DateTime.Now;
    }

    /// <summary>软删除：标记删除，不物理移除数据。</summary>
    public void MarkAsDeleted() => IsDeleted = true;

    public void AddOrderItem(Product product, decimal qty, bool deductStock = true)
    {
        if (deductStock)
            product.ReduceStock(qty);
        else if (qty <= 0)
            throw new DomainException("数量必须大于0");

        // 记录商品名快照，商品后续改名不影响历史订单
        _orderItems.Add(new OrderItem(product.Id, product.Name, qty, product.Price));
    }

    /// <summary>发起支付：待支付订单进入"支付处理中"状态。</summary>
    public void MarkAsPaymentProcessing()
    {
        if (OrderStatus != OrderStatus.Created)
            throw new DomainException("订单状态错误，仅待支付订单可发起支付");

        OrderStatus = OrderStatus.PaymentProcessing;
    }

    /// <summary>确认支付：支付处理中，或支付中超时但库存尚未退回时，可确认为已支付。</summary>
    public void MarkAsPaid()
    {
        if (OrderStatus == OrderStatus.PaymentProcessing
            || (OrderStatus == OrderStatus.TimedOut && !InventoryReturned))
        {
            OrderStatus = OrderStatus.Paid;
            return;
        }

        throw new DomainException("订单状态错误，仅支付处理中订单可确认支付");
    }

    public void Cancel()
    {
        // 已支付 / 已取消 / 已退款 / 超时关闭的订单不可再取消；待支付和支付处理中可取消
        if (OrderStatus is OrderStatus.Paid or OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.TimedOut)
            throw new DomainException("订单已不可取消");

        // 仅未发起支付的订单立刻回补；支付处理中等支付明确失败后再回补，避免超卖
        var restoreInventory = OrderStatus == OrderStatus.Created;
        OrderStatus = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledDomainEvent(this, restoreInventory));
    }

    /// <summary>超时关闭：待支付/支付处理中订单超时未完成支付，系统自动关闭。</summary>
    public void MarkAsTimedOut()
    {
        if (OrderStatus is not (OrderStatus.Created or OrderStatus.PaymentProcessing))
            throw new DomainException("订单状态错误，仅待支付/支付处理中订单可超时关闭");

        var restoreInventory = OrderStatus == OrderStatus.Created;
        OrderStatus = OrderStatus.TimedOut;
        AddDomainEvent(new OrderTimedOutDomainEvent(this, restoreInventory));
    }

    /// <summary>开始回补库存。同一订单只允许成功一次，防止重复加库存。</summary>
    public bool TryBeginInventoryReturn()
    {
        if (InventoryReturned) return false;
        InventoryReturned = true;
        return true;
    }

    /// <summary>退款：仅已支付订单可退款，退款后状态变为已退款。</summary>
    public void MarkAsRefunded()
    {
        if (OrderStatus != OrderStatus.Paid)
            throw new DomainException("订单状态错误，仅已支付订单可退款");

        OrderStatus = OrderStatus.Refunded;
    }
}
