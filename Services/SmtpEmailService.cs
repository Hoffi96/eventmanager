using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelferApp.Services;

/// <summary>
/// Versendet E-Mails per SMTP, sofern in appsettings.json unter "Email"
/// konfiguriert (Enabled = true + SmtpHost gesetzt). Andernfalls wird die
/// Mail nur geloggt, damit die App auch ohne Mail-Setup lauffähig bleibt.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return;
        }

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogInformation(
                "E-Mail nicht versendet (SMTP deaktiviert/nicht konfiguriert) an {To}: {Subject}\n{Body}",
                toAddress, subject, body);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toAddress);

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.SmtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.SmtpUser, _options.SmtpPassword)
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-Mail-Versand an {To} fehlgeschlagen", toAddress);
        }
    }
}
