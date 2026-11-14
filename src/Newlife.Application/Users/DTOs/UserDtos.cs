namespace Newlife.Application.Users.DTOs;

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    IReadOnlyList<string>? Roles);

public sealed record UpdateRolesRequest(IReadOnlyList<string>? Roles);

public sealed record ResetPasswordRequest(string NewPassword);
