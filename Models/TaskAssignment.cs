namespace HelferApp.Models;

public class TaskAssignment
{
    public int TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    /// <summary>true = zentral durch Admin zugeordnet, false = Selbst-Eintragung</summary>
    public bool AssignedByAdmin { get; set; }
}
