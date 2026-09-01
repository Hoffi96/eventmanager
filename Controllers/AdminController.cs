using HelferApp.Data;
using HelferApp.Models;
using HelferApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelferApp.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : AuthorizedControllerBase
{
    private readonly IEmailService _emailService;

    public AdminController(AppDbContext db, IEmailService emailService) : base(db)
    {
        _emailService = emailService;
    }

    public async Task<IActionResult> Users()
    {
        var users = await Db.Users
            .Include(u => u.CoordinatedEvents)
            .ThenInclude(x => x.Event)
            .OrderBy(u => u.Username)
            .ToListAsync();

        ViewBag.Events = await Db.Events.OrderBy(e => e.StartsAt).ToListAsync();
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignCoordinator(int userId, int eventId)
    {
        var user = await Db.Users.FindAsync(userId);
        var ev = await Db.Events.FindAsync(eventId);
        if (user == null || ev == null)
        {
            TempData["Error"] = "Benutzer oder Veranstaltung nicht gefunden.";
            return RedirectToAction(nameof(Users));
        }

        user.IsEventCoordinator = true;

        var exists = await Db.EventCoordinatorAssignments.AnyAsync(x => x.UserId == userId && x.EventId == eventId);
        if (!exists)
        {
            Db.EventCoordinatorAssignments.Add(new EventCoordinatorAssignment
            {
                UserId = userId,
                EventId = eventId,
                AssignedAt = DateTime.UtcNow
            });
        }

        await Db.SaveChangesAsync();
        TempData["Success"] = $"{user.Username} ist jetzt Koordinator:in für '{ev.Name}'.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoordinator(int userId, int eventId)
    {
        var assignment = await Db.EventCoordinatorAssignments.FirstOrDefaultAsync(x => x.UserId == userId && x.EventId == eventId);
        if (assignment == null)
        {
            TempData["Info"] = "Koordinator-Zuordnung nicht gefunden.";
            return RedirectToAction(nameof(Users));
        }

        Db.EventCoordinatorAssignments.Remove(assignment);

        var hasMore = await Db.EventCoordinatorAssignments.AnyAsync(x => x.UserId == userId && x.EventId != eventId);
        var user = await Db.Users.FindAsync(userId);
        if (user != null && !hasMore)
        {
            user.IsEventCoordinator = false;
        }

        await Db.SaveChangesAsync();
        TempData["Info"] = "Koordinator-Zuordnung entfernt.";
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Settings()
    {
        var settings = await Db.AppSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new AppSettings();
            Db.AppSettings.Add(settings);
            await Db.SaveChangesAsync();
        }
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(AppSettings model)
    {
        if (model.EmailEnabled)
        {
            if (string.IsNullOrWhiteSpace(model.SmtpHost))
            {
                ModelState.AddModelError(nameof(model.SmtpHost), "Bitte einen SMTP-Host angeben, wenn der Mailversand aktiviert ist.");
            }

            if (string.IsNullOrWhiteSpace(model.FromAddress))
            {
                ModelState.AddModelError(nameof(model.FromAddress), "Bitte eine Absenderadresse angeben, wenn der Mailversand aktiviert ist.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await Db.AppSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            Db.AppSettings.Add(model);
        }
        else
        {
            existing.EmailEnabled = model.EmailEnabled;
            existing.SmtpHost = model.SmtpHost;
            existing.SmtpPort = model.SmtpPort;
            existing.SmtpUser = model.SmtpUser;
            existing.SmtpPassword = model.SmtpPassword;
            existing.EnableSsl = model.EnableSsl;
            existing.FromAddress = model.FromAddress;
            existing.FromName = model.FromName;
            existing.RemindersEnabled = model.RemindersEnabled;
            existing.Reminder24h = model.Reminder24h;
            existing.Reminder1h = model.Reminder1h;
        }

        await Db.SaveChangesAsync();
        TempData["Success"] = "E-Mail-Einstellungen gespeichert.";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(string testEmailAddress)
    {
        var settings = await Db.AppSettings.FirstOrDefaultAsync() ?? new AppSettings();

        if (string.IsNullOrWhiteSpace(testEmailAddress))
        {
            TempData["Error"] = "Bitte eine Empfängeradresse für die Testmail angeben.";
            return RedirectToAction(nameof(Settings));
        }

        if (!settings.EmailEnabled)
        {
            TempData["Error"] = "Der Mailservice ist deaktiviert. Bitte zuerst aktivieren und speichern.";
            return RedirectToAction(nameof(Settings));
        }

        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            TempData["Error"] = "Bitte zuerst gültige Mail-Einstellungen speichern.";
            return RedirectToAction(nameof(Settings));
        }

        try
        {
            var subject = "HelferApp Testmail";
            var body = $"Dies ist eine Testmail aus der HelferApp.\n\nZeitpunkt: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\nEmpfänger: {testEmailAddress}";
            await _emailService.SendAsync(testEmailAddress.Trim(), subject, body);
            TempData["Success"] = $"Testmail wurde an '{testEmailAddress.Trim()}' versendet.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Testmail konnte nicht gesendet werden: {ex.Message}";
        }

        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(int userId)
    {
        if (userId == CurrentUserId)
        {
            TempData["Error"] = "Du kannst dir selbst die Admin-Rechte nicht entziehen.";
            return RedirectToAction(nameof(Users));
        }

        var user = await Db.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "Benutzer nicht gefunden.";
            return RedirectToAction(nameof(Users));
        }

        user.IsAdmin = !user.IsAdmin;
        await Db.SaveChangesAsync();

        TempData["Success"] = user.IsAdmin
            ? $"{user.Username} ist jetzt Admin."
            : $"Admin-Rechte für {user.Username} entfernt.";

        return RedirectToAction(nameof(Users));
    }
}
