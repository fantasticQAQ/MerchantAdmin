using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Exceptions;

namespace MerchantAdmin.UnitTests.Domain;

public class ProductTests
{
    // ===== 构造校验 =====

    [Fact]
    public void 构造_名称为空_应抛DomainException()
    {
        var act = () => new Product("", 10m, 5m);

        act.Should().Throw<DomainException>().WithMessage("名称不能为空");
    }

    [Fact]
    public void 构造_价格为负_应抛DomainException()
    {
        var act = () => new Product("iPhone", -1m, 5m);

        act.Should().Throw<DomainException>().WithMessage("价格不能为负数");
    }

    [Fact]
    public void 构造_库存为负_应抛DomainException()
    {
        var act = () => new Product("iPhone", 10m, -1m);

        act.Should().Throw<DomainException>().WithMessage("库存不能为负数");
    }

    [Fact]
    public void 构造_合法参数_应成功创建()
    {
        var product = new Product("iPhone", 6999m, 10m);

        product.Name.Should().Be("iPhone");
        product.Price.Should().Be(6999m);
        product.Stock.Should().Be(10m);
    }

    // ===== 扣减库存 =====

    [Fact]
    public void ReduceStock_正常扣减_应减少库存()
    {
        var product = new Product("iPhone", 6999m, 10m);

        product.ReduceStock(3m);

        product.Stock.Should().Be(7m);
    }

    [Fact]
    public void ReduceStock_数量小于等于0_应抛DomainException()
    {
        var product = new Product("iPhone", 6999m, 10m);

        var act = () => product.ReduceStock(0m);

        act.Should().Throw<DomainException>().WithMessage("数量必须大于0");
    }

    [Fact]
    public void ReduceStock_库存不足_应抛DomainException()
    {
        var product = new Product("iPhone", 6999m, 10m);

        var act = () => product.ReduceStock(11m);

        act.Should().Throw<DomainException>().WithMessage("库存不足");
    }

    // ===== 增加库存 =====

    [Fact]
    public void IncreaseStock_应增加库存()
    {
        var product = new Product("iPhone", 6999m, 10m);

        product.IncreaseStock(5m);

        product.Stock.Should().Be(15m);
    }
}
