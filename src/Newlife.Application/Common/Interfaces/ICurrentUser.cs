namespace Newlife.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    string? IpAddress { get; }
}
