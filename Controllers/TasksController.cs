using HelferApp.Data;
using HelferApp.Models;
using HelferApp.Services;
using HelferApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelferApp.Controllers;

[Authorize]
public class TasksController : AuthorizedControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailService _emailService;

    public TasksController(AppDbContext db, IWebHostEnvironment environment, IEmailService emailService)
        : base(db)
    {
        _environment = environment;
        _emailService = emailService;
    }

    public async Task<IActionResult> Index()
    {
        var tasks = await QueryTasksAsync();
        return View(BuildList(tasks).ToList());
    }

    public async Task<IActionResult> Schedule(string filter = "all", int? userId = null)
    {
        var tasks = await QueryTasksAsync();
        List<TaskItem> filteredTasks;

        if (userId.HasValue && userId.Value > 0)
        {
            filteredTasks = tasks.Where(task => task.Assignments.Any(a => a.UserId == userId.Value)).ToList();
        }
        else if (filter == "assigned")
        {
            filteredTasks = tasks.Where(task => task.Assignments.Any(a => a.UserId == CurrentUserId)).ToList();
        }
        else
        {
            filteredTasks = tasks.ToList();
        }

        var model = filteredTasks
            .GroupBy(task => task.StartsAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => new ScheduleDayVm
            {
                Date = group.Key,
                Items = BuildList(group).ToList()
            })
            .ToList();

        ViewBag.Filter = filter == "assigned" ? "assigned" : "all";
        ViewBag.UserId = userId;
        ViewBag.AllUsers = await Db.Users.OrderBy(u => u.Username).ToListAsync();
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var task = await Db.Tasks
            .Include(t => t.Event)
            .Include(t => t.Assignments).ThenInclude(a => a.User)
            .Include(t => t.Comments).ThenInclude(c => c.User)
            .Include(t => t.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(t => t.Waitlist).ThenInclude(w => w.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task is null)
            return NotFound();

        if (!IsAdmin && IsEventCoordinator && !await CanManageEventAsync(task.EventId))
            return Forbid();

        return View(new TaskDetailVm
        {
            Task = task,
            Assignments = task.Assignments.OrderBy(a => a.User.Username).ToList(),
            Comments = task.Comments.OrderBy(c => c.CreatedAt).ToList(),
            Attachments = task.Attachments.OrderByDescending(a => a.CreatedAt).ToList(),
            Waitlist = task.Waitlist.OrderBy(w => w.CreatedAt).ToList(),
            AllUsers = IsAdmin ? await Db.Users.OrderBy(u => u.Username).ToListAsync() : new List<User>(),
            IsAssigned = task.Assignments.Any(a => a.UserId == CurrentUserId),
            IsOnWaitlist = task.Waitlist.Any(w => w.UserId == CurrentUserId),
            SlotsFree = Math.Max(0, task.MaxAssignees - task.Assignments.Count)
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> New()
    {
        await LoadEventsAsync();
        return View("TaskForm", new TaskItem
        {
            StartsAt = DateTime.Now,
            EndsAt = DateTime.Now.AddHours(1),
            MaxAssignees = 1
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(TaskItem form)
    {
        ModelState.Remove(nameof(TaskItem.Event));

        var error = await ValidateTaskAsync(form);
        if (error is not null)
            ModelState.AddModelError(string.Empty, error);

        if (!ModelState.IsValid)
        {
            await LoadEventsAsync();
            return View("TaskForm", form);
        }

        var task = new TaskItem
        {
            EventId = form.EventId,
            Title = form.Title,
            Description = form.Description,
            StartsAt = form.StartsAt,
            EndsAt = form.EndsAt,
            MaxAssignees = form.MaxAssignees,
            CreatedById = CurrentUserId,
            CreatedAt = DateTime.UtcNow
        };

        Db.Tasks.Add(task);
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = task.Id });
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var task = await Db.Tasks.FindAsync(id);
        if (task is null)
            return NotFound();
        if (!IsAdmin && !await CanManageEventAsync(task.EventId))
            return Forbid();

        await LoadEventsAsync();
        return View("TaskForm", task);
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaskItem form)
    {
        ModelState.Remove(nameof(TaskItem.Event));

        if (id != form.Id)
            return BadRequest();

        var existing = await Db.Tasks.FindAsync(id);
        if (existing is null)
            return NotFound();
        if (!IsAdmin && !await CanManageEventAsync(existing.EventId))
            return Forbid();

        var error = await ValidateTaskAsync(form);
        if (error is not null)
            ModelState.AddModelError(string.Empty, error);

        if (!ModelState.IsValid)
        {
            await LoadEventsAsync();
            return View("TaskForm", form);
        }

        existing.EventId = form.EventId;
        existing.Title = form.Title;
        existing.Description = form.Description;
        existing.StartsAt = form.StartsAt;
        existing.EndsAt = form.EndsAt;
        existing.MaxAssignees = form.MaxAssignees;
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await Db.Tasks.FindAsync(id);
        if (task is null)
            return NotFound();
        if (!IsAdmin && !await CanManageEventAsync(task.EventId))
            return Forbid();

        Db.Tasks.Remove(task);
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id)
    {
        var task = await Db.Tasks.Include(t => t.Assignments).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();
        if (task.Assignments.Any(a => a.UserId == CurrentUserId))
            return RedirectToAction(nameof(Details), new { id });

        if (task.Assignments.Count >= task.MaxAssignees)
        {
            if (!await Db.WaitlistEntries.AnyAsync(w => w.TaskId == id && w.UserId == CurrentUserId))
            {
                Db.WaitlistEntries.Add(new WaitlistEntry { TaskId = id, UserId = CurrentUserId, CreatedAt = DateTime.UtcNow });
                await Db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        Db.TaskAssignments.Add(new TaskAssignment { TaskId = id, UserId = CurrentUserId, AssignedAt = DateTime.UtcNow });
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(int id)
    {
        var assignment = await Db.TaskAssignments.FirstOrDefaultAsync(a => a.TaskId == id && a.UserId == CurrentUserId);
        if (assignment is not null)
        {
            Db.TaskAssignments.Remove(assignment);
            await Db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Db.Comments.Add(new Comment { TaskId = id, UserId = CurrentUserId, Body = text.Trim(), CreatedAt = DateTime.UtcNow });
            await Db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUser(int id, int userId)
    {
        var task = await Db.Tasks.Include(t => t.Assignments).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();
        if (!IsAdmin && !await CanManageEventAsync(task.EventId))
            return Forbid();

        var user = await Db.Users.FindAsync(userId);
        if (user is null)
            return NotFound();

        if (!task.Assignments.Any(a => a.UserId == userId) && task.Assignments.Count < task.MaxAssignees)
        {
            Db.TaskAssignments.Add(new TaskAssignment { TaskId = id, UserId = userId, AssignedAt = DateTime.UtcNow });
            await Db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignUser(int id, int userId)
    {
        var task = await Db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();
        if (!IsAdmin && !await CanManageEventAsync(task.EventId))
            return Forbid();

        var assignment = await Db.TaskAssignments.FirstOrDefaultAsync(a => a.TaskId == id && a.UserId == userId);
        if (assignment is not null)
        {
            Db.TaskAssignments.Remove(assignment);
            await Db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Calendar(int id)
    {
        var task = await Db.Tasks.Include(t => t.Event).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        var content = BuildIcs(task);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/calendar", $"task-{id}.ics");
    }

    private async Task<List<TaskItem>> QueryTasksAsync()
    {
        var tasks = await Db.Tasks
            .Include(t => t.Event)
            .Include(t => t.Assignments).ThenInclude(a => a.User)
            .Include(t => t.Waitlist)
            .OrderBy(t => t.StartsAt)
            .ToListAsync();

        if (!IsAdmin && IsEventCoordinator)
            tasks = tasks.Where(task => Db.EventCoordinatorAssignments.Any(x => x.EventId == task.EventId && x.UserId == CurrentUserId)).ToList();

        return tasks;
    }

    private IEnumerable<TaskListItemVm> BuildList(IEnumerable<TaskItem> tasks) => tasks.Select(task => new TaskListItemVm
    {
        Task = task,
        Assignees = task.Assignments.Select(a => a.User).OrderBy(u => u.Username).ToList(),
        SlotsFree = Math.Max(0, task.MaxAssignees - task.Assignments.Count),
        WaitlistCount = task.Waitlist.Count,
        IsAssigned = task.Assignments.Any(a => a.UserId == CurrentUserId),
        IsOnWaitlist = task.Waitlist.Any(w => w.UserId == CurrentUserId)
    });

    private async Task LoadEventsAsync() => ViewBag.Events = await Db.Events.OrderBy(e => e.StartsAt).ToListAsync();

    private async Task<string?> ValidateTaskAsync(TaskItem task)
    {
        if (task.EventId <= 0) return "Bitte eine Veranstaltung auswählen.";
        if (string.IsNullOrWhiteSpace(task.Title)) return "Bitte einen Titel angeben.";
        if (task.EndsAt <= task.StartsAt) return "Ende muss nach dem Start liegen.";
        if (task.MaxAssignees < 1) return "Mindestens 1 Person erforderlich.";

        var ev = await Db.Events.FindAsync(task.EventId);
        if (ev == null) return "Veranstaltung nicht gefunden.";
        if (task.StartsAt < ev.StartsAt || task.EndsAt > ev.EndsAt) return "Der Task-Zeitraum muss vollständig innerhalb der gew\u00e4hlten Veranstaltung liegen.";

        return null;
    }

    private static string BuildIcs(TaskItem task)
    {
        static string Escape(string? value) => (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r", string.Empty)
            .Replace("\n", "\\n");

        var lines = new[]
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//HelferApp//Tasks//DE",
            "BEGIN:VEVENT",
            $"UID:task-{task.Id}@helferapp.local",
            $"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}",
            $"DTSTART:{task.StartsAt.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}",
            $"DTEND:{task.EndsAt.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}",
            $"SUMMARY:{Escape(task.Title)}",
            $"DESCRIPTION:{Escape(task.Description)}",
            $"LOCATION:{Escape(task.Event?.Location)}",
            "END:VEVENT",
            "END:VCALENDAR"
        };

        return string.Join("\r\n", lines) + "\r\n";
    }
}
