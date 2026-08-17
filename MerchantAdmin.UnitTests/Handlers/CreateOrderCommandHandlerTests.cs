using FluentAssertions;
using MediatR;
using MerchantAdmin.Application.Commands;
using MerchantAdmin.Application.Dtos;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Infrastructure;
using MerchantAdmin.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MerchantAdmin.UnitTests.Handlers;

public class CreateOrderCommandHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IDelayJobService> _delayJobMock;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Mock IMediator，让 SaveEntitiesAsync 的领域事件分发不 NRE
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _db = new AppDbContext(options, mediatorMock.Object);
        _delayJobMock = new Mock<IDelayJobService>();

        var cacheMock = new Mock<ICacheService>();
        _handler = new CreateOrderCommandHandler(_db, _delayJobMock.Object, cacheMock.Object);
    }

    [Fact]
    public async Task 正常下单_应扣减库存_返回订单Id_并调度延迟取消()
    {
        // 准备一个库存为 10 的商品
        var product = new Product("iPhone", 6999m, 10m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var cmd = new CreateOrderCommand(
        [
            new OrderItemDto(product.Id, "iPhone", 6999m, 2m)
        ]);

        var orderId = await _handler.Handle(cmd, CancellationToken.None);

        orderId.Should().BeGreaterThan(0);

        // 库存应扣减为 8
        var updatedProduct = await _db.Products.FindAsync(product.Id);
        updatedProduct!.Stock.Should().Be(8m);

        // 订单已写入
        var order = await _db.Orders.FindAsync(orderId);
        order.Should().NotBeNull();
        order!.OrderItems.Should().ContainSingle();

        // 应调度 15 分钟延迟取消
        _delayJobMock.Verify(
            d => d.ScheduleCancelOrderAsync(orderId, TimeSpan.FromMinutes(15)),
            Times.Once);
    }

    [Fact]
    public async Task 商品不存在_应抛DomainException_且不调度延迟取消()
    {
        var cmd = new CreateOrderCommand(
        [
            new OrderItemDto(999, "不存在的商品", 100m, 1m)
        ]);

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("商品不存在");

        // 未调度延迟取消
        _delayJobMock.Verify(
            d => d.ScheduleCancelOrderAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
