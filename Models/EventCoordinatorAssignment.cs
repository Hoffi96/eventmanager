using System.ComponentModel.DataAnnotations;

namespace HelferApp.Models;

public class EventCoordinatorAssignment
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
