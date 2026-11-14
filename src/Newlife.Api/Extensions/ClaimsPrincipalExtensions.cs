using System.Security.Claims;
using Newlife.Application.Common.Exceptions;

namespace Newlife.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("sub")?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedException("auth.invalid_token", "Invalid access token.");
    }
}
