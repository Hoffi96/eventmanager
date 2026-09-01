namespace HelferApp.Services;

public class ReminderOptions
{
    public bool Enabled { get; set; } = true;
    public int MinutesBeforeStart { get; set; } = 120;
    public int PollIntervalMinutes { get; set; } = 5;
}
