using System.Security.Claims;
using HelferApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelferApp.Controllers;

[Authorize]
public abstract class AuthorizedControllerBase : Controller
{
    protected readonly AppDbContext Db;

    protected AuthorizedControllerBase(AppDbContext db)
    {
        Db = db;
    }

    protected int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    protected bool IsAdmin => User.IsInRole("Admin");
    protected bool IsEventCoordinator => User.IsInRole("EventCoordinator");

    protected async Task<bool> CanManageEventAsync(int eventId)
    {
        return IsAdmin || await Db.EventCoordinatorAssignments.AnyAsync(x => x.EventId == eventId && x.UserId == CurrentUserId);
    }

    protected async Task<bool> CanManageTaskAsync(int taskId)
    {
        return IsAdmin || await Db.Tasks.AnyAsync(t => t.Id == taskId && Db.EventCoordinatorAssignments.Any(x => x.EventId == t.EventId && x.UserId == CurrentUserId));
    }
}
