using FluentAssertions;
using MediatR;
using MerchantAdmin.Application.Commands;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using MerchantAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MerchantAdmin.UnitTests.Handlers;

public class SearchQueryTests : IDisposable
{
    private readonly AppDbContext _db;

    public SearchQueryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _db = new AppDbContext(options, mediatorMock.Object);
    }

    [Fact]
    public async Task 商品搜索_按名称过滤_只返回匹配商品()
    {
        _db.Products.Add(new Product("iPhone", 6999m, 10m));
        _db.Products.Add(new Product("MacBook", 9999m, 5m));
        await _db.SaveChangesAsync();

        var handler = new GetAllProductsQueryHandler(_db);

        var result = await handler.Handle(new GetAllProductsQuery("iPhone"), CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items.Single().Name.Should().Be("iPhone");
    }

    [Fact]
    public async Task 商品查询_分页_只返回当前页数据()
    {
        for (var i = 1; i <= 5; i++)
        {
            _db.Products.Add(new Product($"商品{i}", 10m, 10m));
        }
        await _db.SaveChangesAsync();

        var handler = new GetAllProductsQueryHandler(_db);

        var result = await handler.Handle(new GetAllProductsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.Total.Should().Be(5);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task 订单搜索_按状态过滤_只返回匹配订单()
    {
        var product = new Product("iPhone", 6999m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // 一个已支付订单
        var paidOrder = new Order();
        paidOrder.AddOrderItem(product, 1m);
        paidOrder.MarkAsPaymentProcessing();
        paidOrder.MarkAsPaid();
        _db.Orders.Add(paidOrder);

        // 一个待支付订单
        var createdOrder = new Order();
        createdOrder.AddOrderItem(product, 1m);
        _db.Orders.Add(createdOrder);

        await _db.SaveChangesAsync();

        var handler = new GetAllOrdersQueryHandler(_db);

        var paidResult = await handler.Handle(new GetAllOrdersQuery(Status: "Paid"), CancellationToken.None);

        paidResult.Total.Should().Be(1);
        paidResult.Items.Should().ContainSingle();
        paidResult.Items.Single().OrderId.Should().Be(paidOrder.Id);
        paidResult.Items.Single().OrderStatus.Should().Be(OrderStatus.Paid);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
