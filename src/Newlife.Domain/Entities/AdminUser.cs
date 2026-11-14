using Microsoft.AspNetCore.Identity;

namespace Newlife.Domain.Entities;

public sealed class AdminUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
}
