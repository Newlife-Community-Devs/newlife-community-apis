namespace Newlife.Application.Settings.DTOs;

public sealed record UpdateSettingRequest(string Value);

public sealed record SettingResponse(string Key, string Value, string Group);
