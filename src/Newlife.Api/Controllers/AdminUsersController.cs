using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Auth.DTOs;
using Newlife.Application.Common.Models;
using Newlife.Application.Users;
using Newlife.Application.Users.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController(AdminUserService adminUserService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserResponse>>> List(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
        => Ok(await adminUserService.ListAsync(query, cancellationToken));

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await adminUserService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> UpdateRoles(
        Guid id,
        UpdateRolesRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminUserService.UpdateRolesAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await adminUserService.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/revoke-sessions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken cancellationToken)
    {
        await adminUserService.RevokeSessionsAsync(id, cancellationToken);
        return NoContent();
    }
}
