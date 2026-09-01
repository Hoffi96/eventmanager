namespace HelferApp.Models;

public class Event
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Location { get; set; }
    public string Description { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<TaskItem> Tasks { get; set; } = new();
    public List<EventAttachment> Attachments { get; set; } = new();
    public List<EventCoordinatorAssignment> Coordinators { get; set; } = new();
}
