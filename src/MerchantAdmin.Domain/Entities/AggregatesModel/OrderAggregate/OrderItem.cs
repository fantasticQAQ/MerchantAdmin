
namespace MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

public class OrderItem
    : Entity
{
    public int ProductId { get; private set; }

    /// <summary>下单时的商品名快照（商品后续改名不影响历史订单）。</summary>
    public string ProductName { get; private set; }

    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }

    protected OrderItem() { }

    public OrderItem(int productId, string productName, decimal quantity, decimal price)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        Price = price;
    }
}
