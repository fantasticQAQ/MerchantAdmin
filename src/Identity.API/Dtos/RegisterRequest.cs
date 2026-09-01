namespace Identity.API.Dtos
{
    public record RegisterRequest(string UserName, string Password, string? Email);
}
