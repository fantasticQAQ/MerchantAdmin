using FluentAssertions;
using MediatR;
using MerchantAdmin.Application.Commands;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MerchantAdmin.UnitTests.Handlers;

public class DeleteOrderCommandHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DeleteOrderCommandHandler _handler;

    public DeleteOrderCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _db = new AppDbContext(options, mediatorMock.Object);

        _handler = new DeleteOrderCommandHandler(_db);
    }

    [Fact]
    public async Task 已取消订单_应软删除成功()
    {
        var order = await CreateOrder(OrderStatus.Cancelled);

        var result = await _handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.Should().BeTrue();
        // 软删除：数据保留，标记 IsDeleted
        var deleted = await _db.Orders.FindAsync(order.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task 待支付订单_应拒绝删除_支付可能在途()
    {
        var order = await CreateOrder(OrderStatus.Created);

        var act = async () => await _handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task 支付处理中订单_应拒绝删除_支付可能在途()
    {
        var order = await CreateOrder(OrderStatus.PaymentProcessing);

        var act = async () => await _handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task 已支付订单_应软删除成功_且不影响库存()
    {
        // 下单扣了库存：100 → 99；删除订单是纯归档，不改变库存
        var order = await CreateOrder(OrderStatus.Paid);
        var product = await _db.Products.FirstAsync();
        product.Stock.Should().Be(99m);

        var result = await _handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.Should().BeTrue();
        var updated = await _db.Products.FindAsync(product.Id);
        updated!.Stock.Should().Be(99m);
    }

    [Fact]
    public async Task 已退款订单_应软删除成功()
    {
        var order = await CreateOrder(OrderStatus.Refunded);

        var result = await _handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task 超时关闭订单_应软删除成功_且不影响库存()
    {
        // 下单扣减 100→99；删除订单是纯归档，不改变库存（超时关闭的回补由领域事件负责）
        var order = await CreateOrder(OrderStatus.TimedOut);
        var product = await _db.Products.FirstAsync();
        product.Stock.Should().Be(99m);

        var result = await _handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.Should().BeTrue();
        var updated = await _db.Products.FindAsync(product.Id);
        updated!.Stock.Should().Be(99m);
    }

    [Fact]
    public async Task 商品已被物理删除的订单_删除应正常归档()
    {
        var order = await CreateOrder(OrderStatus.Paid);
        var product = await _db.Products.FirstAsync();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.Should().BeTrue();
        var deleted = await _db.Orders.FindAsync(order.Id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task 订单不存在_应返回false()
    {
        var result = await _handler.Handle(new DeleteOrderCommand(999), CancellationToken.None);

        result.Should().BeFalse();
    }

    private async Task<Order> CreateOrder(OrderStatus status)
    {
        var product = new Product("测试商品", 10m, 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m);

        // 设置目标状态
        if (status == OrderStatus.Cancelled)
            order.Cancel();
        else if (status == OrderStatus.PaymentProcessing)
            order.MarkAsPaymentProcessing();
        else if (status == OrderStatus.Paid)
        {
            order.MarkAsPaymentProcessing();
            order.MarkAsPaid();
        }
        else if (status == OrderStatus.Refunded)
        {
            order.MarkAsPaymentProcessing();
            order.MarkAsPaid();
            order.MarkAsRefunded();
        }
        else if (status == OrderStatus.TimedOut)
        {
            order.MarkAsTimedOut();
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
