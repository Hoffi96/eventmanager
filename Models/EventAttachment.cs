namespace HelferApp.Models;

public class EventAttachment
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string FileName { get; set; } = "";
    public string OriginalName { get; set; } = "";

    public int UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
