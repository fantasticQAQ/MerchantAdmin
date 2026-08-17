using FluentAssertions;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Domain.Events;
using MerchantAdmin.Domain.Exceptions;

namespace MerchantAdmin.UnitTests.Domain;

public class OrderTests
{
    // ===== 创建 =====

    [Fact]
    public void 新建订单_状态应为Created_且无订单项()
    {
        var order = new Order();

        order.OrderStatus.Should().Be(OrderStatus.Created);
        order.OrderItems.Should().BeEmpty();
    }

    // ===== 添加订单项 =====

    [Fact]
    public void AddOrderItem_应扣减商品库存_并记录订单项()
    {
        var product = new Product("iPhone", 6999m, 10m);
        var order = new Order();

        order.AddOrderItem(product, 2m);

        product.Stock.Should().Be(8m);
        order.OrderItems.Should().ContainSingle();
        order.OrderItems.Single().ProductId.Should().Be(product.Id);
        order.OrderItems.Single().Quantity.Should().Be(2m);
        order.OrderItems.Single().Price.Should().Be(6999m);
    }

    [Fact]
    public void AddOrderItem_库存不足_应抛DomainException()
    {
        var product = new Product("iPhone", 6999m, 1m);
        var order = new Order();

        var act = () => order.AddOrderItem(product, 2m);

        act.Should().Throw<DomainException>().WithMessage("库存不足");
    }

    // ===== 支付状态机 =====

    [Fact]
    public void MarkAsPaymentProcessing_待支付订单_应变为支付处理中()
    {
        var order = new Order();

        order.MarkAsPaymentProcessing();

        order.OrderStatus.Should().Be(OrderStatus.PaymentProcessing);
    }

    [Fact]
    public void MarkAsPaymentProcessing_已支付订单_应抛DomainException()
    {
        var order = new Order();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();

        var act = order.MarkAsPaymentProcessing;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkAsPaid_支付处理中订单_应变为已支付()
    {
        var order = new Order();
        order.MarkAsPaymentProcessing();

        order.MarkAsPaid();

        order.OrderStatus.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void MarkAsPaid_待支付订单未发起支付_应抛DomainException()
    {
        var order = new Order();

        var act = order.MarkAsPaid;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkAsPaid_已取消订单_应抛DomainException()
    {
        var order = new Order();
        order.Cancel();

        var act = order.MarkAsPaid;

        act.Should().Throw<DomainException>();
    }

    // ===== 取消 =====

    [Fact]
    public void Cancel_待支付状态_应变为已取消_并产生领域事件()
    {
        var order = new Order();

        order.Cancel();

        order.OrderStatus.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledDomainEvent);
    }

    [Fact]
    public void Cancel_支付处理中订单_应变为已取消()
    {
        var order = new Order();
        order.MarkAsPaymentProcessing();

        order.Cancel();

        order.OrderStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_已支付订单_应抛DomainException()
    {
        var order = new Order();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();

        var act = order.Cancel;

        act.Should().Throw<DomainException>().WithMessage("订单已不可取消");
    }

    [Fact]
    public void Cancel_已取消订单_应抛DomainException()
    {
        var order = new Order();
        order.Cancel();

        var act = order.Cancel;

        act.Should().Throw<DomainException>();
    }

    // ===== 超时关闭 =====

    [Fact]
    public void MarkAsTimedOut_待支付订单_应变为超时关闭_并产生领域事件()
    {
        var order = new Order();

        order.MarkAsTimedOut();

        order.OrderStatus.Should().Be(OrderStatus.TimedOut);
        order.DomainEvents.Should().ContainSingle(e => e is OrderTimedOutDomainEvent);
    }

    [Fact]
    public void MarkAsTimedOut_支付处理中订单_应变为超时关闭()
    {
        var order = new Order();
        order.MarkAsPaymentProcessing();

        order.MarkAsTimedOut();

        order.OrderStatus.Should().Be(OrderStatus.TimedOut);
    }

    [Fact]
    public void MarkAsTimedOut_已支付订单_应抛DomainException()
    {
        var order = new Order();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();

        var act = order.MarkAsTimedOut;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_超时关闭订单_应抛DomainException()
    {
        var order = new Order();
        order.MarkAsTimedOut();

        var act = order.Cancel;

        act.Should().Throw<DomainException>().WithMessage("订单已不可取消");
    }

    [Fact]
    public void Cancel_已退款订单_应抛DomainException()
    {
        var order = new Order();
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        order.MarkAsRefunded();

        var act = order.Cancel;

        act.Should().Throw<DomainException>().WithMessage("订单已不可取消");
    }
}
