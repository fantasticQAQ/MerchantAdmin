using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

namespace MerchantAdmin.UnitTests.Domain;

public class AddressTests
{
    [Fact]
    public void 两个相同字段的地址_应相等()
    {
        var address1 = new Address("长安街1号", "北京", "北京", "中国", "100000");
        var address2 = new Address("长安街1号", "北京", "北京", "中国", "100000");

        address1.Should().Be(address2);
    }

    [Fact]
    public void 任一字段不同_应不相等()
    {
        var address1 = new Address("长安街1号", "北京", "北京", "中国", "100000");
        var address2 = new Address("长安街2号", "北京", "北京", "中国", "100000");

        address1.Should().NotBe(address2);
    }

    [Fact]
    public void 相同地址_哈希码应相同()
    {
        var address1 = new Address("长安街1号", "北京", "北京", "中国", "100000");
        var address2 = new Address("长安街1号", "北京", "北京", "中国", "100000");

        address1.GetHashCode().Should().Be(address2.GetHashCode());
    }
}
