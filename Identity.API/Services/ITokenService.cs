using IdentityService.WebAPI.Entities;

namespace IdentityService.WebAPI.Services
{
    public interface ITokenService
    {
        string CreateToken(ApplicationUser user);
    }
}
