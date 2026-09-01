using System.Security.Claims;
using HelferApp.Data;
using HelferApp.Models;
using HelferApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelferApp.Controllers;

[Authorize]
public class EventsController : AuthorizedControllerBase
{
    private readonly IWebHostEnvironment _env;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".txt",
        ".doc", ".docx", ".xls", ".xlsx", ".csv", ".zip"
    };

    private const long MaxFileSize = 10 * 1024 * 1024;

    public EventsController(AppDbContext db, IWebHostEnvironment env) : base(db)
    {
        _env = env;
    }

    private string UploadDir
    {
        get
        {
            var dir = Path.Combine(_env.ContentRootPath, "Uploads", "events");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public async Task<IActionResult> Index()
    {
        IQueryable<Event> query = Db.Events
            .Include(e => e.Tasks)
            .ThenInclude(t => t.Assignments)
            .Include(e => e.Coordinators)
            .ThenInclude(c => c.User)
            .OrderBy(e => e.StartsAt);

        if (!IsAdmin && IsEventCoordinator)
        {
            query = query.Where(e => e.Coordinators.Any(c => c.UserId == CurrentUserId));
        }

        var events = await query.ToListAsync();
        return View(events);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await Db.Events
            .Include(e => e.Tasks).ThenInclude(t => t.Assignments).ThenInclude(a => a.User)
            .Include(e => e.Tasks).ThenInclude(t => t.Waitlist)
            .Include(e => e.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(e => e.Coordinators).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
        {
            return NotFound();
        }

        if (!IsAdmin && IsEventCoordinator && !ev.Coordinators.Any(c => c.UserId == CurrentUserId))
        {
            return Forbid();
        }

        return View(ev);
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpGet]
    public IActionResult New()
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        return View("EventForm", new Event());
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(Event form)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        var error = ValidateEvent(form);
        if (error != null)
        {
            TempData["Error"] = error;
            return View("EventForm", form);
        }

        var ev = new Event
        {
            Name = form.Name.Trim(),
            Location = string.IsNullOrWhiteSpace(form.Location) ? null : form.Location.Trim(),
            Description = HtmlSanitizer.Sanitize(form.Description),
            StartsAt = form.StartsAt,
            EndsAt = form.EndsAt,
            CreatedById = CurrentUserId,
            CreatedAt = DateTime.UtcNow
        };

        Db.Events.Add(ev);
        await Db.SaveChangesAsync();

        TempData["Success"] = "Veranstaltung angelegt.";
        return RedirectToAction(nameof(Details), new { id = ev.Id });
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await Db.Events.FindAsync(id);
        if (ev == null)
        {
            return NotFound();
        }

        if (!await CanManageEventAsync(id))
        {
            return Forbid();
        }

        return View("EventForm", ev);
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Event form)
    {
        var ev = await Db.Events.FindAsync(id);
        if (ev == null)
        {
            return NotFound();
        }

        if (!await CanManageEventAsync(id))
        {
            return Forbid();
        }

        var error = ValidateEvent(form);
        if (error != null)
        {
            TempData["Error"] = error;
            form.Id = id;
            return View("EventForm", form);
        }

        ev.Name = form.Name.Trim();
        ev.Location = string.IsNullOrWhiteSpace(form.Location) ? null : form.Location.Trim();
        ev.Description = HtmlSanitizer.Sanitize(form.Description);
        ev.StartsAt = form.StartsAt;
        ev.EndsAt = form.EndsAt;

        await Db.SaveChangesAsync();

        TempData["Success"] = "Veranstaltung aktualisiert.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await Db.Events.Include(e => e.Tasks).FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null)
        {
            return NotFound();
        }

        if (!await CanManageEventAsync(id))
        {
            return Forbid();
        }

        if (ev.Tasks.Any())
        {
            TempData["Error"] = "Veranstaltung hat noch zugeordnete Tasks und kann nicht gelöscht werden. Bitte zuerst die Tasks löschen oder verschieben.";
            return RedirectToAction(nameof(Details), new { id });
        }

        Db.Events.Remove(ev);
        await Db.SaveChangesAsync();

        TempData["Info"] = "Veranstaltung gelöscht.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpGet]
    public async Task<IActionResult> ExportCsv(int id)
    {
        var ev = await Db.Events
            .Include(e => e.Tasks).ThenInclude(t => t.Assignments).ThenInclude(a => a.User)
            .Include(e => e.Tasks).ThenInclude(t => t.Waitlist).ThenInclude(w => w.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null)
        {
            return NotFound();
        }

        if (!await CanManageEventAsync(id))
        {
            return Forbid();
        }

        var lines = new List<string>
        {
            "Event;Task;Beginn;Ende;Slots;Zugewiesen;Freie Plätze;Warteliste;Helfende"
        };

        foreach (var task in ev.Tasks.OrderBy(t => t.StartsAt))
        {
            var helpers = string.Join(", ", task.Assignments.OrderBy(a => a.User.Username).Select(a => a.User.Username));
            lines.Add(string.Join(';', new[]
            {
                Csv(ev.Name),
                Csv(task.Title),
                task.StartsAt.ToString("dd.MM.yyyy HH:mm"),
                task.EndsAt.ToString("dd.MM.yyyy HH:mm"),
                task.MaxAssignees.ToString(),
                task.Assignments.Count.ToString(),
                (task.MaxAssignees - task.Assignments.Count).ToString(),
                task.Waitlist.Count.ToString(),
                Csv(helpers)
            }));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
        return File(bytes, "text/csv; charset=utf-8", $"event-{id}-helferliste.csv");
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Attachment(int id, IFormFile? file)
    {
        if (!await CanManageEventAsync(id))
        {
            return Forbid();
        }

        var exists = await Db.Events.AnyAsync(e => e.Id == id);
        if (!exists)
        {
            return NotFound();
        }

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Bitte eine Datei auswählen.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (file.Length > MaxFileSize)
        {
            TempData["Error"] = "Datei ist zu groß (max. 10 MB).";
            return RedirectToAction(nameof(Details), new { id });
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            TempData["Error"] = "Dateityp nicht erlaubt.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(UploadDir, storedName);
        await using (var fs = System.IO.File.Create(path))
        {
            await file.CopyToAsync(fs);
        }

        Db.EventAttachments.Add(new EventAttachment
        {
            EventId = id,
            OriginalName = Path.GetFileName(file.FileName),
            FileName = storedName,
            UploadedById = CurrentUserId,
            CreatedAt = DateTime.UtcNow
        });
        await Db.SaveChangesAsync();

        TempData["Success"] = "Datei hochgeladen.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin,EventCoordinator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
    {
        if (!await CanManageEventAsync(id))
        {
            return Forbid();
        }

        var attachment = await Db.EventAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.EventId == id);
        if (attachment == null)
        {
            return NotFound();
        }

        var path = Path.Combine(UploadDir, attachment.FileName);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }

        Db.EventAttachments.Remove(attachment);
        await Db.SaveChangesAsync();

        TempData["Info"] = "Anhang gelöscht.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static string Csv(string? value)
    {
        return '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';
    }

    private static string? ValidateEvent(Event form)
    {
        if (string.IsNullOrWhiteSpace(form.Name))
        {
            return "Bitte einen Namen angeben.";
        }

        if (form.EndsAt <= form.StartsAt)
        {
            return "Ende muss nach dem Start liegen.";
        }

        return null;
    }
}
