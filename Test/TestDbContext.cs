using Microsoft.EntityFrameworkCore;

namespace Test
{
    public class TestEntity
    {
        public int Id { get; set; }
    }
    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
       : base(options)
        {
        }
        public DbSet<TestEntity> TestEntities { get; set; }
    }
}
