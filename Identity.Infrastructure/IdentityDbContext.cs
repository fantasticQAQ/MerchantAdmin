using Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure
{
    public class IdentityDbContext
     : IdentityDbContext<ApplicationUser, ApplicationRole, long>
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) { }

    }
}
