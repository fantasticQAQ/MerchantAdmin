using FluentAssertions;
using MediatR;
using MerchantAdmin.Application.DomainEventHandlers;
using MerchantAdmin.Application.IntegrationEvents;
using MerchantAdmin.Application.Services;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Domain.Events;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace MerchantAdmin.UnitTests.Handlers;

public class OrderTimeoutProcessorTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OrderTimeoutProcessor _processor;
    private readonly Mock<IMediator> _mediatorMock;

    public OrderTimeoutProcessorTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options, new Mock<IMediator>().Object);

        // 模拟 MediatR 领域事件分发：超时关闭事件 → 执行真实回补 handler（与生产行为一致）
        var cacheMock = new Mock<ICacheService>();
        var handlerLogger = new Mock<ILogger<OrderCancelledDomainEventHandler>>();
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Callback<INotification, CancellationToken>((notification, ct) =>
            {
                if (notification is OrderTimedOutDomainEvent evt)
                {
                    var handler = new OrderCancelledDomainEventHandler(_db, cacheMock.Object, handlerLogger.Object);
                    handler.Handle(evt, ct).GetAwaiter().GetResult();
                }
            })
            .Returns(Task.CompletedTask);

        _db = new AppDbContext(options, mediatorMock.Object);
        _mediatorMock = mediatorMock;
        _processor = new OrderTimeoutProcessor(
            _db,
            new Mock<IOrderingIntegrationEventService>().Object,
            new Mock<ILogger<OrderTimeoutProcessor>>().Object);
    }

    [Fact]
    public async Task 待支付订单_超时应标记为TimedOut_并回补库存()
    {
        var product = new Product("可乐", 4m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 2m); // 10 → 8
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _processor.ProcessOrderAsync(order.Id, CancellationToken.None);

        _mediatorMock.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        var updated = await _db.Orders.FindAsync(order.Id);
        updated!.OrderStatus.Should().Be(OrderStatus.TimedOut);
        (await _db.Products.FindAsync(product.Id))!.Stock.Should().Be(10m);
    }

    [Fact]
    public async Task 支付处理中订单_超时应标记为TimedOut()
    {
        var product = new Product("可乐", 4m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m);
        order.MarkAsPaymentProcessing();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _processor.ProcessOrderAsync(order.Id, CancellationToken.None);

        var updated = await _db.Orders.FindAsync(order.Id);
        updated!.OrderStatus.Should().Be(OrderStatus.TimedOut);
    }

    [Fact]
    public async Task 已支付订单_超时处理应跳过_保持Paid()
    {
        var product = new Product("可乐", 4m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m);
        order.MarkAsPaymentProcessing();
        order.MarkAsPaid();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _processor.ProcessOrderAsync(order.Id, CancellationToken.None);

        var updated = await _db.Orders.FindAsync(order.Id);
        updated!.OrderStatus.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task 已取消订单_超时处理应跳过_保持Cancelled()
    {
        var product = new Product("可乐", 4m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m);
        order.Cancel();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _processor.ProcessOrderAsync(order.Id, CancellationToken.None);

        var updated = await _db.Orders.FindAsync(order.Id);
        updated!.OrderStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task 订单不存在_应不抛异常()
    {
        var act = async () => await _processor.ProcessOrderAsync(999, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
