namespace Newlife.Application.Prayers.DTOs;

public sealed record SubmitPrayerRequest(
    string? Title,
    string Content,
    string? SubmitterName,
    string? PhoneNumber,
    bool IsAnonymous);

public sealed record PublicPrayerResponse(
    Guid Id,
    string? Title,
    string Content,
    string? SubmitterName,
    bool IsAnonymous,
    bool IsAnswered,
    DateTime? AnsweredAt,
    DateTime CreatedAt);

public sealed record AdminPrayerResponse(
    Guid Id,
    string? Title,
    string Content,
    string? SubmitterName,
    string? PhoneNumber,
    bool IsAnonymous,
    bool IsAnswered,
    DateTime? AnsweredAt,
    bool IsFlagged,
    string? FlagReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record PrayerStatsResponse(
    int Total,
    int Open,
    int Answered,
    int Flagged,
    int Anonymous,
    int SubmissionsLast7Days,
    int UniqueSubmitters);

public sealed record AnswerPrayerRequest(string? Note);

public sealed record FlagPrayerRequest(string? Reason);
