using MerchantAdmin.Domain.Events;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Domain.Seedwork;
using MerchantAdmin.Ordering.Domain.Seedwork;

namespace MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

public class Order : Entity, IAggregateRoot
{
    public DateTime CreatedAt { get; private set; }
    public OrderStatus OrderStatus { get; private set; }

    private readonly List<OrderItem> _OrderItems;
    public IReadOnlyCollection<OrderItem> OrderItems => _OrderItems.AsReadOnly();

    public Order()
    {
        _OrderItems = new List<OrderItem>();
        OrderStatus = OrderStatus.Created;
        CreatedAt = DateTime.Now;
    }


    public void AddOrderItem(Product product, decimal qty)
    {
        product.ReduceStock(qty);
        _OrderItems.Add(new OrderItem(product.Id, qty, product.Price));
    }

    public void MarkAsPaid()
    {
        //if (OrderStatus != OrderStatus.Created)
        //    throw new DomainException("订单状态错误");

        OrderStatus = OrderStatus.Paid;
    }

    public void Cancel()
    {
        if (OrderStatus != OrderStatus.Created)
            throw new DomainException("订单已不可取消");

        OrderStatus = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledDomainEvent(this));
    }
}
