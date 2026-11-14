using Newlife.Domain.Entities;

namespace Newlife.Application.Common.Interfaces;

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(AdminUser user, IEnumerable<string> roles);

    RefreshTokenValue CreateRefreshToken();

    string HashRefreshToken(string token);

    TimeSpan RefreshTokenLifetime { get; }

    TimeSpan RotationGracePeriod { get; }
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public sealed record RefreshTokenValue(string Token, string Hash, DateTime ExpiresAt);
