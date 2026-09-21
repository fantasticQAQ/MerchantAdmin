using MerchantAdmin.Domain.Entities;

namespace MerchantAdmin.Infrastructure.EntityConfigurations
{
    class ClientRequestEntityTypeConfiguration : IEntityTypeConfiguration<ClientRequest>
    {
        public void Configure(EntityTypeBuilder<ClientRequest> builder)
        {
            builder.ToTable("ClientRequests");
            // Id 主键唯一：并发重复请求时由数据库兜底拦截
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Time).IsRequired();
        }
    }
}
