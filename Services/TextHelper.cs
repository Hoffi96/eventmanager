using System.Net;
using System.Text.RegularExpressions;

namespace HelferApp.Services;

/// <summary>Hilfsfunktionen für Views: Klartext-Vorschau aus WYSIWYG-HTML erzeugen.</summary>
public static class TextHelper
{
    public static string StripHtml(string? html, int maxLength = 0)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (maxLength > 0 && text.Length > maxLength)
        {
            text = text.Substring(0, maxLength) + "...";
        }

        return text;
    }
}
