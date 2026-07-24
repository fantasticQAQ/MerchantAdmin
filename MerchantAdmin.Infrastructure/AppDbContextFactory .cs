using Microsoft.EntityFrameworkCore.Design;
namespace MerchantAdmin.Infrastructure
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer("Server=127.0.0.1,1433;Database=MerchantAdmin;User Id=sa;Password=YourStrong!Passw0rd123;TrustServerCertificate=True;MultipleActiveResultSets=true");

            // 设计时：Mediator 传 null
            return new AppDbContext(optionsBuilder.Options, null!);
        }
    }
}
