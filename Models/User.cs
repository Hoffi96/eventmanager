namespace HelferApp.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool IsEventCoordinator { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public List<EventCoordinatorAssignment> CoordinatedEvents { get; set; } = new();
}
