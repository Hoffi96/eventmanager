using HelferApp.Data;
using HelferApp.Models;
using HelferApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=helferapp.db"));

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection("Reminders"));
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
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
