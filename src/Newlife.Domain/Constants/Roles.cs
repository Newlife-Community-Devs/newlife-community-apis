namespace Newlife.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string ContentEditor = "ContentEditor";
    public const string EventManager = "EventManager";

    public static readonly string[] All = [Admin, ContentEditor, EventManager];
}
