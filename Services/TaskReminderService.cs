using HelferApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelferApp.Services;

public class TaskReminderService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TaskReminderService> _logger;
    private readonly ReminderOptions _options;

    public TaskReminderService(
        IServiceProvider services,
        IOptions<ReminderOptions> options,
        ILogger<TaskReminderService> logger)
    {
        _services = services;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Task reminders disabled.");
            return;
        }

        var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.PollIntervalMinutes)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending task reminders.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var utcNow = DateTime.UtcNow;
        var from = utcNow.AddMinutes(_options.MinutesBeforeStart - Math.Max(1, _options.PollIntervalMinutes));
        var to = utcNow.AddMinutes(_options.MinutesBeforeStart + Math.Max(1, _options.PollIntervalMinutes));

        var tasks = await db.Tasks
            .Include(t => t.Event)
            .Include(t => t.Assignments)
            .ThenInclude(a => a.User)
            .Where(t => t.StartsAt >= from && t.StartsAt <= to)
            .ToListAsync(cancellationToken);

        foreach (var task in tasks)
        {
            foreach (var assignment in task.Assignments.Where(a => !a.AssignedByAdmin || a.User.Email != null))
            {
                if (string.IsNullOrWhiteSpace(assignment.User.Email))
                {
                    continue;
                }

                var alreadySent = await db.Comments.AnyAsync(c =>
                    c.TaskId == task.Id &&
                    c.UserId == assignment.UserId &&
                    c.Body == $"[system-reminder:{task.StartsAt:O}]",
                    cancellationToken);

                if (alreadySent)
                {
                    continue;
                }

                var body = $"Hallo {assignment.User.Username},\n\nErinnerung: Dein Helfer-Task '{task.Title}' für '{task.Event.Name}' beginnt bald.\n\nBeginn: {task.StartsAt.ToLocalTime():dd.MM.yyyy HH:mm}\nEnde: {task.EndsAt.ToLocalTime():dd.MM.yyyy HH:mm}\n\nBitte prüfe bei Bedarf deine Zuordnung in Helfer-Tasks.";
                await email.SendAsync(assignment.User.Email, $"Erinnerung: {task.Title}", body);

                db.Comments.Add(new Models.Comment
                {
                    TaskId = task.Id,
                    UserId = assignment.UserId,
                    Body = $"[system-reminder:{task.StartsAt:O}]",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
