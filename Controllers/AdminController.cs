using HelferApp.Data;
using HelferApp.Models;
using HelferApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelferApp.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : AuthorizedControllerBase
{
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;

    public AdminController(
        AppDbContext db,
        IOptionsMonitor<EmailOptions> emailOptions) : base(db)
    {
        _emailOptions = emailOptions;
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

        var hasMore = await Db.EventCoordinatorAssignments.AnyAsync(x => x.UserId == userId && !(x.UserId == userId && x.EventId == eventId));
        var user = await Db.Users.FindAsync(userId);
        if (user != null && !hasMore)
        {
            user.IsEventCoordinator = false;
        }

        await Db.SaveChangesAsync();
        TempData["Info"] = "Koordinator-Zuordnung entfernt.";
        return RedirectToAction(nameof(Users));
    }

    public IActionResult Settings() => View(_emailOptions.CurrentValue);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Settings(EmailOptions model)
    {
        if (model.Enabled)
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

        TempData["Info"] = "E-Mail-Einstellungen werden derzeit über appsettings.*.json verwaltet. Bitte die Konfigurationsdatei anpassen und die App neu starten.";
        return View(model);
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
