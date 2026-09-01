using HelferApp.Models;

namespace HelferApp.ViewModels;

public class TaskListItemVm
{
    public required TaskItem Task { get; set; }
    public List<User> Assignees { get; set; } = new();
    public int SlotsFree { get; set; }
    public bool IsAssigned { get; set; }
    public bool IsOnWaitlist { get; set; }
    public int WaitlistCount { get; set; }
}

public class TaskDetailVm
{
    public required TaskItem Task { get; set; }
    public List<TaskAssignment> Assignments { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = new();
    public List<WaitlistEntry> Waitlist { get; set; } = new();
    public int SlotsFree { get; set; }
    public bool IsAssigned { get; set; }
    public bool IsOnWaitlist { get; set; }
    public List<User> AllUsers { get; set; } = new();
}

public class ScheduleDayVm
{
    public DateTime Date { get; set; }
    public List<TaskListItemVm> Items { get; set; } = new();
}
