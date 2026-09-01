using MediatR;
using MerchantAdmin.API.Application.DomainEventHandlers;
using MerchantAdmin.API.Infrastructure.Caching;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Domain.Events;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MerchantAdmin.UnitTests.Handlers;

public class OrderCancelledDomainEventHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OrderCancelledDomainEventHandler _handler;

    public OrderCancelledDomainEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options, new Mock<IMediator>().Object);
        _handler = new OrderCancelledDomainEventHandler(
            _db,
            new Mock<ICacheService>().Object,
            new Mock<ILogger<OrderCancelledDomainEventHandler>>().Object);
    }

    [Fact]
    public async Task 取消订单_商品存在_应回补库存()
    {
        var product = new Product("可乐", 4m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 2m); // 10 → 8
        order.Cancel();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _handler.Handle(new OrderCancelledDomainEvent(order), CancellationToken.None);

        (await _db.Products.FindAsync(product.Id))!.Stock.Should().Be(10m);
    }

    [Fact]
    public async Task 取消订单_商品已被物理删除_应跳过且不抛异常()
    {
        var product = new Product("可乐", 4m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 2m); // 10 → 8
        order.Cancel();

        // 商品被物理删除（历史数据异常场景）
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        var act = async () => await _handler.Handle(new OrderCancelledDomainEvent(order), CancellationToken.None);

        // 不抛异常，回补跳过（Error 日志供对账）
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task 超时关闭订单_商品存在_应回补库存()
    {
        var product = new Product("可乐", 4m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order();
        order.AddOrderItem(product, 1m); // 10 → 9
        order.MarkAsTimedOut();
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _handler.Handle(new OrderTimedOutDomainEvent(order), CancellationToken.None);

        (await _db.Products.FindAsync(product.Id))!.Stock.Should().Be(10m);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
