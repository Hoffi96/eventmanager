using System.Text.RegularExpressions;

namespace HelferApp.Services;

/// <summary>
/// Einfache, regelbasierte Bereinigung von HTML aus dem WYSIWYG-Editor (Quill).
/// Nur Admins können aktuell Task-/Event-Beschreibungen speichern, trotzdem
/// als Verteidigungslinie: entfernt gefährliche Tags, Event-Handler-Attribute
/// und javascript:-Links. Kein Ersatz für eine vollwertige Sanitizer-Bibliothek,
/// aber ohne zusätzliche NuGet-Abhängigkeit nutzbar.
/// </summary>
public static class HtmlSanitizer
{
    private static readonly string[] ForbiddenTags =
    {
        "script", "iframe", "object", "embed", "form", "input", "link", "meta", "style", "svg"
    };

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var result = html;

        foreach (var tag in ForbiddenTags)
        {
            result = Regex.Replace(result, $"<{tag}[^>]*>.*?</{tag}>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, $"<{tag}[^>]*/?>", "", RegexOptions.IgnoreCase);
        }

        // Event-Handler-Attribute (onclick=, onerror= ...) entfernen
        result = Regex.Replace(result, @"\s+on\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", "", RegexOptions.IgnoreCase);

        // javascript:-Links entschärfen
        result = Regex.Replace(result, @"(href|src)\s*=\s*(""|')\s*javascript:[^""']*(""|')", "$1=\"#\"", RegexOptions.IgnoreCase);

        result = result.Trim();

        // Quill liefert bei leerem Editor "<p><br></p>" statt eines leeren Strings.
        if (result == "<p><br></p>")
        {
            return "";
        }

        return result;
    }
}
