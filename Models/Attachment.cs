namespace HelferApp.Models;

public class Attachment
{
    public int Id { get; set; }

    public int TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    /// <summary>Name, unter dem die Datei physisch im Uploads-Ordner liegt (eindeutig).</summary>
    public string FileName { get; set; } = "";

    /// <summary>Ursprünglicher Dateiname, wie er dem Nutzer angezeigt wird.</summary>
    public string OriginalName { get; set; } = "";

    public int UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
