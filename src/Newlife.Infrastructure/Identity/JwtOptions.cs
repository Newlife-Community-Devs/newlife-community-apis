namespace Newlife.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "newlife-community-apis";

    public string Audience { get; set; } = "newlife-website";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 30;

    public int RefreshRotationGraceSeconds { get; set; } = 60;
}
