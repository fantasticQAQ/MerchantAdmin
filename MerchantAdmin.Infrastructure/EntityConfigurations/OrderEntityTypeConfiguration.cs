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

            // 【核心关键】告诉 EF Core 直接去读写私有字段 _orderItems
            builder.Metadata.FindNavigation(nameof(Order.OrderItems)).SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
