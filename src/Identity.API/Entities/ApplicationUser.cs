namespace Identity.API.Entities
{
    public class ApplicationUser : IdentityUser<long>
    {
        public ApplicationUser(string userName, string? email) : base(userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("用户名不能为空", nameof(userName));

            UserName = userName;
            Email = email;
        }
    }
}
