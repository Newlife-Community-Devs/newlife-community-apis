using Microsoft.Extensions.Logging;
using Newlife.Application.Common.Interfaces;

namespace Newlife.Infrastructure.Email;

public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        logger.LogInformation("Email suppressed (provider None). Would send '{Subject}' to {Recipient}.", subject, to);
        return Task.CompletedTask;
    }
}
