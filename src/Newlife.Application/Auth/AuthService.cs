using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newlife.Application.Auth.DTOs;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Domain.Entities;

namespace Newlife.Application.Auth;

public sealed class AuthService(
    UserManager<AdminUser> userManager,
    IAppDbContext db,
    IJwtTokenService jwtTokenService)
{
    public async Task<AuthTokensResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            throw new UnauthorizedException("auth.invalid_credentials", "Invalid email or password.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new LockedOutException();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException("auth.invalid_credentials", "Invalid email or password.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthTokensResponse> RefreshAsync(RefreshRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var hash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var stored = await db.Set<RefreshToken>()
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            throw new UnauthorizedException("auth.invalid_refresh_token", "Invalid refresh token.");
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            throw new UnauthorizedException("auth.invalid_refresh_token", "Invalid refresh token.");
        }

        var now = DateTime.UtcNow;

        if (stored.RevokedAt is not null)
        {
            var withinGrace = now - stored.RevokedAt.Value <= jwtTokenService.RotationGracePeriod;
            if (withinGrace && stored.ReplacedByTokenHash is not null)
            {
                var successor = await db.Set<RefreshToken>()
                    .FirstOrDefaultAsync(token => token.TokenHash == stored.ReplacedByTokenHash, cancellationToken);

                if (successor is not null && successor.RevokedAt is null && successor.ExpiresAt > now)
                {
                    return await IssueTokensAsync(user, ipAddress, cancellationToken);
                }
            }

            await RevokeAllAsync(user.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("auth.refresh_reuse", "Refresh token reuse detected. Please sign in again.");
        }

        if (stored.ExpiresAt <= now)
        {
            stored.RevokedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("auth.refresh_expired", "Refresh token has expired.");
        }

        var replacement = jwtTokenService.CreateRefreshToken();
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = replacement.Hash;

        db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = replacement.Hash,
            ExpiresAt = replacement.ExpiresAt,
            CreatedByIp = ipAddress
        });

        await db.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.CreateAccessToken(user, roles);

        return new AuthTokensResponse(
            accessToken.Token,
            replacement.Token,
            accessToken.ExpiresAt,
            ToUserResponse(user, roles));
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var hash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var stored = await db.Set<RefreshToken>()
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserResponse> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new UnauthorizedException("auth.invalid_token", "User no longer exists.");
        }

        var roles = await userManager.GetRolesAsync(user);
        return ToUserResponse(user, roles);
    }

    private async Task<AuthTokensResponse> IssueTokensAsync(
        AdminUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.CreateAccessToken(user, roles);
        var refreshToken = jwtTokenService.CreateRefreshToken();

        db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.Hash,
            ExpiresAt = refreshToken.ExpiresAt,
            CreatedByIp = ipAddress
        });

        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokensResponse(
            accessToken.Token,
            refreshToken.Token,
            accessToken.ExpiresAt,
            ToUserResponse(user, roles));
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

    private static UserResponse ToUserResponse(AdminUser user, IEnumerable<string> roles)
        => new(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToArray(),
            user.LockoutEnd > DateTime.UtcNow);
}
