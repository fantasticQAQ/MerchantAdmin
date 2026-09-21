using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Identity.API.Data
{
    public class IdentityDbContext
     : IdentityDbContext<ApplicationUser, ApplicationRole, long>
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) { }

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(e =>
            {
                e.ToTable("RefreshTokens");
                e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
                e.Property(x => x.SecurityStamp).HasMaxLength(128).IsRequired();
                // 唯一索引：校验时按哈希精确查找，同时兜住极端情况下的重复签发
                e.HasIndex(x => x.TokenHash).IsUnique();
                e.HasIndex(x => x.UserId);
                // 用户被删除时一并清理其 refresh token
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
