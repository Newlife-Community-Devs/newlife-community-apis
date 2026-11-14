using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Newlife.Api.Extensions;
using Newlife.Application.Auth;
using Newlife.Application.Auth.DTOs;
using Newlife.Application.Common.Interfaces;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthTokensResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
        => Ok(await authService.LoginAsync(request, currentUser.IpAddress, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthTokensResponse>> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken)
        => Ok(await authService.RefreshAsync(request, currentUser.IpAddress, cancellationToken));

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
        => Ok(await authService.GetMeAsync(User.GetUserId(), cancellationToken));
}
