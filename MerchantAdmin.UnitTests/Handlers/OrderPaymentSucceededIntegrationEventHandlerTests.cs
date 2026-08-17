using FluentAssertions;
using MediatR;
using MerchantAdmin.Application.IntegrationEvents.EventHandling;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.EventBus.Events;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace MerchantAdmin.UnitTests.Handlers;

public class OrderPaymentSucceededIntegrationEventHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OrderPaymentSucceededIntegrationEventHandler _handler;

    public OrderPaymentSucceededIntegrationEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _db = new AppDbContext(options, mediatorMock.Object);

        var loggerMock = new Mock<ILogger<OrderPaymentSucceededIntegrationEventHandler>>();
        _handler = new OrderPaymentSucceededIntegrationEventHandler(_db, loggerMock.Object);
    }

    [Fact]
    public async Task 收到支付事件_支付处理中订单_应标记为已支付()
    {
        // 准备一个"支付处理中"状态的订单（需要先有一个商品来满足订单项）
        var product = new Product("iPhone", 6999m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m);
        order.MarkAsPaymentProcessing();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var evt = new OrderPaymentSucceededIntegrationEvent(order.Id);

        await _handler.Handle(evt);

        var updated = await _db.Orders.FindAsync(order.Id);
        updated!.OrderStatus.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task 收到支付事件_订单不存在_应不抛异常()
    {
        var evt = new OrderPaymentSucceededIntegrationEvent(999);

        var act = () => _handler.Handle(evt);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task 收到支付事件_订单已取消_应幂等跳过_保持已取消状态()
    {
        var product = new Product("iPhone", 6999m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m);
        order.Cancel();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var evt = new OrderPaymentSucceededIntegrationEvent(order.Id);

        await _handler.Handle(evt);

        var updated = await _db.Orders.FindAsync(order.Id);
        updated!.OrderStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task 收到支付事件_订单已软删除_应忽略回调_不改变状态()
    {
        var product = new Product("iPhone", 6999m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m);
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        order.MarkAsDeleted();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var evt = new OrderPaymentSucceededIntegrationEvent(order.Id);

        await _handler.Handle(evt);

        var updated = await _db.Orders.FindAsync(order.Id);
        updated!.OrderStatus.Should().Be(OrderStatus.Paid);
        updated!.IsDeleted.Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
