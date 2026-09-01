namespace HelferApp.Models;

/// <summary>
/// Wartelisten-Eintrag für einen vollen Task. Bei freiwerdendem Platz rückt
/// der älteste Eintrag (nach CreatedAt) automatisch nach.
/// </summary>
public class WaitlistEntry
{
    public int Id { get; set; }

    public int TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
