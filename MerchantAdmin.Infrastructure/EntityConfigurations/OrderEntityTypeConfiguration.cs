using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MerchantAdmin.Infrastructure.EntityConfigurations
{
    class OrderEntityTypeConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.Ignore(b => b.DomainEvents);

            builder
                .Property(o => o.OrderStatus)
                .HasConversion<string>()
                .HasMaxLength(30);

            // 乐观并发令牌：更新时若 RowVersion 已变化则抛 DbUpdateConcurrencyException，
            // 避免并发写入互相覆盖（如支付回调覆盖取消操作）
            builder.Property(o => o.RowVersion).IsRowVersion();

            // 【核心关键】告诉 EF Core 直接去读写私有字段 _orderItems
            builder.Metadata.FindNavigation(nameof(Order.OrderItems)).SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
