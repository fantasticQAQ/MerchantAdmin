using MerchantAdmin.API.Application.Commands;
using MerchantAdmin.API.Application.Dtos;
using MerchantAdmin.API.Application.Validators;

namespace MerchantAdmin.UnitTests.Validators;

public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    [Fact]
    public void 合法商品_应校验通过()
    {
        var cmd = new CreateProductCommand(new ProductDto(0, "iPhone", 6999m, 10m, true));

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void 商品名为空_应校验失败()
    {
        var cmd = new CreateProductCommand(new ProductDto(0, "", 6999m, 10m, true));

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProductDto.Name");
    }

    [Fact]
    public void 价格为0_应校验失败()
    {
        var cmd = new CreateProductCommand(new ProductDto(0, "iPhone", 0m, 10m, true));

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProductDto.Price");
    }

    [Fact]
    public void 库存为负_应校验失败()
    {
        var cmd = new CreateProductCommand(new ProductDto(0, "iPhone", 6999m, -1m, true));

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProductDto.Stock");
    }
}

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public void 合法订单_应校验通过()
    {
        var cmd = new CreateOrderCommand(
        [
            new OrderItemDto(1, "iPhone", 6999m, 2m)
        ]);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void 订单项为空列表_应校验失败()
    {
        var cmd = new CreateOrderCommand([]);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrderItems");
    }

    [Fact]
    public void 订单项商品Id无效_应校验失败()
    {
        var cmd = new CreateOrderCommand(
        [
            new OrderItemDto(0, "iPhone", 6999m, 2m)
        ]);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrderItems[0].ProductId");
    }

    [Fact]
    public void 订单项数量为0_应校验失败()
    {
        var cmd = new CreateOrderCommand(
        [
            new OrderItemDto(1, "iPhone", 6999m, 0m)
        ]);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OrderItems[0].Quantity");
    }
}

public class IdCommandValidatorsTests
{
    [Fact]
    public void 取消订单_合法Id_应校验通过()
    {
        var validator = new CancelOrderCommandValidator();
        var result = validator.Validate(new CancelOrderCommand(1));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void 取消订单_Id为0_应校验失败()
    {
        var validator = new CancelOrderCommandValidator();
        var result = validator.Validate(new CancelOrderCommand(0));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void 支付订单_Id为负_应校验失败()
    {
        var validator = new PayOrderCommandValidator();
        var result = validator.Validate(new PayOrderCommand(-1));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void 删除商品_Id为0_应校验失败()
    {
        var validator = new DeleteProductCommandValidator();
        var result = validator.Validate(new DeleteProductCommand(0));
        result.IsValid.Should().BeFalse();
    }
}
