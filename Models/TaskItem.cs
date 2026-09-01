namespace HelferApp.Models;

public class TaskItem
{
    public int Id { get; set; }

    /// <summary>Jeder Task muss genau einer Veranstaltung zugeordnet sein.</summary>
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// Admin-gesteuertes Limit: wie viele Personen diesem Task maximal
    /// zugeordnet werden können.
    /// </summary>
    public int MaxAssignees { get; set; } = 1;

    /// <summary>Zeitraum, in dem dieser Task erledigt werden soll.</summary>
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public int? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<TaskAssignment> Assignments { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = new();
    public List<WaitlistEntry> Waitlist { get; set; } = new();
}
