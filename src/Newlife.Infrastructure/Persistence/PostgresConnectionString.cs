using System.Text;

namespace Newlife.Infrastructure.Persistence;

public static class PostgresConnectionString
{
    private static readonly Dictionary<string, string> QueryKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sslmode"] = "SSL Mode",
        ["channel_binding"] = "Channel Binding",
        ["connect_timeout"] = "Timeout",
        ["timeout"] = "Command Timeout",
        ["application_name"] = "Application Name",
        ["search_path"] = "Search Path",
        ["trust_server_certificate"] = "Trust Server Certificate",
        ["include_realm"] = "Include Realm",
        ["persist_security_info"] = "Persist Security Info",
    };

    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || !IsUri(connectionString))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);

        var username = string.Empty;
        var password = string.Empty;

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            username = separator < 0
                ? Uri.UnescapeDataString(uri.UserInfo)
                : Uri.UnescapeDataString(uri.UserInfo[..separator]);
            password = separator < 0
                ? string.Empty
                : Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
        }

        var parts = new List<KeyValuePair<string, string>>
        {
            new("Host", uri.Host),
            new("Port", (uri.Port > 0 ? uri.Port : 5432).ToString()),
            new("Username", username),
            new("Password", password),
        };

        var database = uri.AbsolutePath.Trim('/');
        if (database.Length > 0)
        {
            parts.Add(new KeyValuePair<string, string>("Database", database));
        }

        var query = uri.Query.TrimStart('?');

        if (query.Length > 0)
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..separator]);
                var value = Uri.UnescapeDataString(pair[(separator + 1)..]);

                if (QueryKeyMap.TryGetValue(key, out var mapped) && !string.IsNullOrEmpty(value))
                {
                    parts.Add(new KeyValuePair<string, string>(mapped, value));
                }
            }
        }

        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            builder.Append(part.Key)
                .Append('=')
                .Append(QuoteIfNeeded(part.Value))
                .Append(';');
        }

        return builder.ToString();
    }

    private static bool IsUri(string value)
        => value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static string QuoteIfNeeded(string value)
    {
        if (value.Length > 0 && value.IndexOfAny([';', '=', '\'', ' ', '"']) < 0 && value == value.Trim())
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
