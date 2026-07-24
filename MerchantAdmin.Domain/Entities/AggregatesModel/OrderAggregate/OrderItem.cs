using MerchantAdmin.Ordering.Domain.Seedwork;

namespace MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

public class OrderItem
    : Entity
{
    public int ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }

    protected OrderItem() { }

    public OrderItem(int productId, decimal quantity, decimal price)
    {
        ProductId = productId;
        Quantity = quantity;
        Price = price;
    }
}
