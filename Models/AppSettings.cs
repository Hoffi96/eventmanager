using System.ComponentModel.DataAnnotations;

namespace HelferApp.Models;

public class AppSettings
{
    public int Id { get; set; }

    [Display(Name = "E-Mail-Versand aktiviert")]
    public bool EmailEnabled { get; set; }

    [Display(Name = "SMTP-Host")]
    public string? SmtpHost { get; set; }

    [Display(Name = "SMTP-Port")]
    public int SmtpPort { get; set; } = 587;

    [Display(Name = "SMTP-Benutzer")]
    public string? SmtpUser { get; set; }

    [Display(Name = "SMTP-Passwort")]
    public string? SmtpPassword { get; set; }

    [Display(Name = "SSL verwenden")]
    public bool EnableSsl { get; set; } = true;

    [Display(Name = "Absenderadresse")]
    public string? FromAddress { get; set; }

    [Display(Name = "Absendername")]
    public string? FromName { get; set; }

    [Display(Name = "Erinnerungen aktiviert")]
    public bool RemindersEnabled { get; set; }

    [Display(Name = "Erinnerung 24h vorher")]
    public bool Reminder24h { get; set; }

    [Display(Name = "Erinnerung 1h vorher")]
    public bool Reminder1h { get; set; }
}
