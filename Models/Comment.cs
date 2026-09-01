namespace HelferApp.Models;

public class Comment
{
    public int Id { get; set; }

    public int TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
