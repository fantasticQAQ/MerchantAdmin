using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MerchantAdmin.Infrastructure
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                ?? "Server=127.0.0.1,1433;Database=MerchantAdmin.Merchant;User Id=sa;Password=P@ssw0rd2024!;TrustServerCertificate=True;MultipleActiveResultSets=true";

            optionsBuilder.UseSqlServer(connectionString);

            // 设计时：Mediator 传 null
            return new AppDbContext(optionsBuilder.Options, null!);
        }
    }
}
