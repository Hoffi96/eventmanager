using System.Security.Claims;
using System.Security.Cryptography;
using HelferApp.Data;
using HelferApp.Models;
using HelferApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelferApp.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public AccountController(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string username, string email, string password, string password2)
    {
        username = (username ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            TempData["Error"] = "Benutzername und Passwort sind erforderlich.";
        }
        else if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            TempData["Error"] = "Bitte eine gültige E-Mail-Adresse angeben.";
        }
        else if (password != password2)
        {
            TempData["Error"] = "Passwörter stimmen nicht überein.";
        }
        else if (password.Length < 4)
        {
            TempData["Error"] = "Passwort ist zu kurz (mind. 4 Zeichen).";
        }
        else if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            TempData["Error"] = "Benutzername bereits vergeben.";
        }
        else if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            TempData["Error"] = "Für diese E-Mail-Adresse existiert bereits ein Konto.";
        }
        else
        {
            _db.Users.Add(new User
            {
                Username = username,
                Email = email,
                PasswordHash = PasswordHelper.Hash(password),
                IsAdmin = false,
                IsEventCoordinator = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Konto erstellt. Du kannst dich jetzt einloggen.";
            return RedirectToAction(nameof(Login));
        }

        return View();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user != null && PasswordHelper.Verify(password, user.PasswordHash))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
            };

            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }
            if (user.IsEventCoordinator)
            {
                claims.Add(new Claim(ClaimTypes.Role, "EventCoordinator"));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            TempData["Success"] = $"Willkommen, {user.Username}!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Tasks");
        }

        TempData["Error"] = "Login fehlgeschlagen.";
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Info"] = "Du wurdest abgemeldet.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return Challenge();
        }

        return View(user);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string username, string email, bool notifyOnAssignment, bool notify24hBeforeTask, bool notify1hBeforeTask)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return Challenge();
        }

        username = (username ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            TempData["Error"] = "Bitte einen Benutzernamen angeben.";
            return View(user);
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            TempData["Error"] = "Bitte eine gültige E-Mail-Adresse angeben.";
            return View(user);
        }

        if (await _db.Users.AnyAsync(u => u.Id != user.Id && u.Username == username))
        {
            TempData["Error"] = "Benutzername bereits vergeben.";
            return View(user);
        }

        if (await _db.Users.AnyAsync(u => u.Id != user.Id && u.Email == email))
        {
            TempData["Error"] = "Für diese E-Mail-Adresse existiert bereits ein Konto.";
            return View(user);
        }

        user.Username = username;
        user.Email = email;
        user.NotifyOnAssignment = notifyOnAssignment;
        user.Notify24hBeforeTask = notify24hBeforeTask;
        user.Notify1hBeforeTask = notify1hBeforeTask;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Profil aktualisiert. Bitte neu einloggen, damit der Anzeigename im Menü aktualisiert wird.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string newPassword2)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return Challenge();
        }

        if (!PasswordHelper.Verify(currentPassword, user.PasswordHash))
        {
            TempData["Error"] = "Aktuelles Passwort ist falsch.";
        }
        else if (newPassword.Length < 4)
        {
            TempData["Error"] = "Neues Passwort ist zu kurz (mind. 4 Zeichen).";
        }
        else if (newPassword != newPassword2)
        {
            TempData["Error"] = "Neue Passwörter stimmen nicht überein.";
        }
        else
        {
            user.PasswordHash = PasswordHelper.Hash(newPassword);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Passwort geändert.";
            return RedirectToAction("Index", "Tasks");
        }

        return View();
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        email = (email ?? string.Empty).Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            user.PasswordResetToken = Convert.ToHexString(bytes);
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(2);
            await _db.SaveChangesAsync();

            var resetUrl = Url.Action(nameof(ResetPassword), "Account", new { token = user.PasswordResetToken }, Request.Scheme) ?? "";
            var body = $"Hallo {user.Username},\n\num dein Passwort für Helfer-Tasks zurückzusetzen, öffne bitte diesen Link:\n{resetUrl}\n\nDer Link ist 2 Stunden gültig.";
            await _emailService.SendAsync(user.Email, "Passwort zurücksetzen - Helfer-Tasks", body);
        }

        TempData["Info"] = "Falls die E-Mail-Adresse existiert, wurde ein Link zum Zurücksetzen versendet.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Der Link ist ungültig oder abgelaufen.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token && u.PasswordResetTokenExpiresAt > DateTime.UtcNow);
        if (user == null)
        {
            TempData["Error"] = "Der Link ist ungültig oder abgelaufen.";
            return RedirectToAction(nameof(Login));
        }

        ViewData["Token"] = token;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string token, string newPassword, string newPassword2)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token && u.PasswordResetTokenExpiresAt > DateTime.UtcNow);
        if (user == null)
        {
            TempData["Error"] = "Der Link ist ungültig oder abgelaufen.";
            return RedirectToAction(nameof(Login));
        }

        if (newPassword.Length < 4)
        {
            TempData["Error"] = "Passwort ist zu kurz (mind. 4 Zeichen).";
            ViewData["Token"] = token;
            return View();
        }

        if (newPassword != newPassword2)
        {
            TempData["Error"] = "Passwörter stimmen nicht überein.";
            ViewData["Token"] = token;
            return View();
        }

        user.PasswordHash = PasswordHelper.Hash(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Passwort wurde zurückgesetzt. Du kannst dich jetzt einloggen.";
        return RedirectToAction(nameof(Login));
    }
}
