namespace IdentityService.WebAPI.Dtos
{
    public record RegisterRequest(string UserName, string Password, string Email);
}
