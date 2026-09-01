using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Identity.API.Data
{
    public class IdentityDbContext
     : IdentityDbContext<ApplicationUser, ApplicationRole, long>
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) { }

    }
}
