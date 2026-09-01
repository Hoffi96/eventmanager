using System.Net;
using System.Net.Mail;
using HelferApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HelferApp.Services;

/// <summary>
/// Versendet E-Mails per SMTP, sofern in den AppSettings Enabled = true
/// und SmtpHost gesetzt ist. Andernfalls wird die Mail nur geloggt.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IDbContextFactory<AppDbContext> dbFactory, ILogger<SmtpEmailService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var settings = await db.AppSettings.FirstOrDefaultAsync();

        if (settings == null || !settings.EmailEnabled || string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            _logger.LogInformation(
                "E-Mail nicht versendet (SMTP deaktiviert/nicht konfiguriert) an {To}: {Subject}\n{Body}",
                toAddress, subject, body);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName ?? "Helfer-Tasks"),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toAddress);

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(settings.SmtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.SmtpUser, settings.SmtpPassword)
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
