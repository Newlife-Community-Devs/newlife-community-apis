using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newlife.Application.Auth.DTOs;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Common.Models;
using Newlife.Application.Users.DTOs;
using Newlife.Domain.Constants;
using Newlife.Domain.Entities;

namespace Newlife.Application.Users;

public sealed class AdminUserService(
    UserManager<AdminUser> userManager,
    IAppDbContext db)
{
    public async Task<PagedResult<UserResponse>> ListAsync(PaginationQuery pagination, CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsNoTracking().OrderBy(user => user.Email);
        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            items.Add(ToResponse(user, roles));
        }

        return PagedResult<UserResponse>.Create(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var roles = NormalizeRoles(request.Roles);

        var user = new AdminUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new ConflictException("user.create_failed", string.Join(" ", createResult.Errors.Select(error => error.Description)));
        }

        if (roles.Count > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded)
            {
                throw new ConflictException("user.role_assignment_failed", string.Join(" ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        return ToResponse(user, roles);
    }

    public async Task<UserResponse> UpdateRolesAsync(Guid userId, UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        var newRoles = NormalizeRoles(request.Roles);
        var currentRoles = await userManager.GetRolesAsync(user);

        if (currentRoles.Contains(Roles.Admin) && !newRoles.Contains(Roles.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
            {
                throw new ConflictException("user.last_admin", "Cannot remove the last admin.");
            }
        }

        var toRemove = currentRoles.Except(newRoles).ToArray();
        if (toRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                throw new ConflictException("user.role_update_failed", string.Join(" ", removeResult.Errors.Select(error => error.Description)));
            }
        }

        var toAdd = newRoles.Except(currentRoles).ToArray();
        if (toAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                throw new ConflictException("user.role_update_failed", string.Join(" ", addResult.Errors.Select(error => error.Description)));
            }
        }

        await RevokeAllAsync(user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(user, newRoles);
    }

    public async Task ResetPasswordAsync(Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            throw new ConflictException("user.password_reset_failed", string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        await RevokeAllAsync(user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        await RevokeAllAsync(user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AdminUser> FindUserAsync(Guid userId)
        => await userManager.FindByIdAsync(userId.ToString())
           ?? throw new NotFoundException("user.not_found", "User was not found.");

    private static IReadOnlyList<string> NormalizeRoles(IReadOnlyList<string>? roles)
    {
        var requested = (roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var invalid = requested.Where(role => !Roles.All.Contains(role, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (invalid.Length > 0)
        {
            throw new BadRequestException("user.invalid_role", $"Unknown role(s): {string.Join(", ", invalid)}.");
        }

        return requested
            .Select(role => Roles.All.First(known => known.Equals(role, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await db.Set<RefreshToken>()
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    private static UserResponse ToResponse(AdminUser user, IEnumerable<string> roles)
        => new(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToArray(),
            user.LockoutEnd > DateTime.UtcNow);
}
