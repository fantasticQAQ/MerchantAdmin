using Identity.API.Entities;
using Identity.API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.API
{
    public class IdentityDbContext
     : IdentityDbContext<ApplicationUser, ApplicationRole, long>
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) { }

    }
}
