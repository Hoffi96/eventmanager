using System.ComponentModel.DataAnnotations;

namespace HelferApp.Services;

/// <summary>Bindet an den "Email"-Abschnitt in appsettings.json.</summary>
public class EmailOptions
{
    /// <summary>Wenn false, werden E-Mails nur geloggt statt versendet (Standard, kein Setup nötig).</summary>
    public bool Enabled { get; set; }

    [Display(Name = "SMTP-Host")]
    public string SmtpHost { get; set; } = "";

    [Display(Name = "SMTP-Port")]
    [Range(1, 65535, ErrorMessage = "Bitte einen gültigen Port zwischen 1 und 65535 angeben.")]
    public int SmtpPort { get; set; } = 587;

    [Display(Name = "SMTP-Benutzer")]
    public string SmtpUser { get; set; } = "";

    [Display(Name = "SMTP-Passwort")]
    [DataType(DataType.Password)]
    public string SmtpPassword { get; set; } = "";

    [Display(Name = "SSL/TLS verwenden")]
    public bool EnableSsl { get; set; } = true;

    [Display(Name = "Absenderadresse")]
    [EmailAddress(ErrorMessage = "Bitte eine gültige Absenderadresse angeben.")]
    public string FromAddress { get; set; } = "";

    [Display(Name = "Absendername")]
    public string FromName { get; set; } = "Helfer-Tasks";
}
