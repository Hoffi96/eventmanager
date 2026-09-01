using HelferApp.Data;
using HelferApp.Models;
using HelferApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var dbPath = builder.Environment.IsDevelopment()
    ? "helferapp.db"
    : "/app/data/helferapp.db";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/app/data/keys"))
        .SetApplicationName("eventmanagement");
}

builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection("Reminders"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddHostedService<TaskReminderService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = PasswordHelper.Hash("admin123"),
            IsAdmin = true,
            IsEventCoordinator = false,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        Console.WriteLine(new string('=', 60));
        Console.WriteLine("Erststart: Admin-Account angelegt");
        Console.WriteLine("  Benutzername: admin");
        Console.WriteLine("  Passwort:     admin123");
        Console.WriteLine("  E-Mail:       admin@example.com (bitte per Passwort-Reset anpassen)");
        Console.WriteLine("  -> Bitte nach dem ersten Login das Passwort ändern!");
        Console.WriteLine(new string('=', 60));
    }

    // AppSettings initialisieren, falls noch nicht vorhanden
    if (!db.AppSettings.Any())
    {
        db.AppSettings.Add(new AppSettings
        {
            EmailEnabled = false,
            SmtpHost = "",
            SmtpPort = 587,
            SmtpUser = "",
            SmtpPassword = "",
            EnableSsl = true,
            FromAddress = "no-reply@example.com",
            FromName = "Helfer-Tasks",
            RemindersEnabled = false,
            Reminder24h = false,
            Reminder1h = false
        });
        db.SaveChanges();
    }
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Tasks}/{action=Index}/{id?}");

app.Run();
