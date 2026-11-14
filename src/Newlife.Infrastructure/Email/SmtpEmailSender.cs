using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Newlife.Application.Common.Interfaces;

namespace Newlife.Infrastructure.Email;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"] ?? string.Empty;
        var port = configuration.GetValue("Email:SmtpPort", 587);
        var user = configuration["Email:SmtpUser"];
        var password = configuration["Email:SmtpPassword"];
        var from = configuration["Email:FromAddress"] ?? "no-reply@nlcbcgh.com";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrEmpty(user)
                ? null
                : new NetworkCredential(user, password)
        };

        using var mail = new MailMessage(from, to, subject, htmlBody)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(mail, cancellationToken);
    }
}
