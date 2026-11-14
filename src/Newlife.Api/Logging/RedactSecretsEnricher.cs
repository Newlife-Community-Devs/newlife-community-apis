using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Newlife.Api.Logging;

public sealed partial class RedactSecretsEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.TryGetValue("RequestPath", out var value) ||
            value is not ScalarValue { Value: string path } ||
            !path.Contains("token=", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var redacted = TokenPattern().Replace(path, "$1***");
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestPath", redacted));
    }

    [GeneratedRegex(@"(?i)(token=)[^&]*")]
    private static partial Regex TokenPattern();
}
