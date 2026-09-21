using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MerchantAdmin.Domain.Entities.AggregatesModel;
using MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

namespace MerchantAdmin.Infrastructure.EntityConfigurations
{
    class ProductEntityTypeConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            // 乐观并发令牌：更新时若 RowVersion 已变化则抛 DbUpdateConcurrencyException，
            builder.Property(o => o.RowVersion).IsRowVersion();

            builder.ToTable(t => t.HasCheckConstraint("CK_Products_Stock_NonNegative", "[Stock] >= 0"));
        }
    }
}
