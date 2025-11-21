using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContractMonthlyClaimSystem.Models;
using ContractMonthlyClaimSystem.Services;

// alias to avoid collision with your model Claim
using SecClaim = System.Security.Claims.Claim;

namespace ContractMonthlyClaimSystem.Controllers;

public class AccountController : Controller
{
    private readonly IUserStore _users;

    public AccountController(IUserStore users)
    {
        _users = users;
    }

    // ---------- LOGIN ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (TempData["RegistrationMessage"] is string msg)
        {
            ViewBag.RegistrationMessage = msg;
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Find by email + role
        var user = await _users.FindByEmailAndRoleAsync(model.Email, model.Role);
        if (user == null || !await _users.ValidatePasswordAsync(model.Email, model.Password))
        {
            ModelState.AddModelError(string.Empty, "Invalid email, password or role.");
            return View(model);
        }

        // NEW: block lecturers that HR has not approved yet
        if (user.Role == UserRole.Lecturer && !user.IsApproved)
        {
            ModelState.AddModelError(string.Empty,
                "Your account has been created but is waiting for HR approval. " +
                "You will be able to log in once HR activates your profile.");
            return View(model);
        }

        await SignInAsync(user);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // Role-based redirect
        return user.Role switch
        {
            UserRole.Lecturer => RedirectToAction("MyClaims", "Lecturer"),
            UserRole.Coordinator => RedirectToAction("Index", "Coordinator"),
            UserRole.Manager => RedirectToAction("Index", "Manager"),
            UserRole.HR => RedirectToAction("Index", "Hr"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    // ---------- REGISTER (Lecturer self-registration) ----------

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Make sure email is unique
        var allUsers = await _users.GetAllAsync();
        if (allUsers.Any(u => u.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(string.Empty, "An account with this email already exists.");
            return View(model);
        }

        // Create lecturer as NOT approved yet
        var lecturer = new AppUser
        {
            Id = Guid.NewGuid(),
            Name = model.Name,
            Email = model.Email,
            Role = UserRole.Lecturer,
            PasswordHash = HashPassword(model.Password),
            HourlyRate = 350m,   // default – HR can later change this
            IsApproved = false   // *** key line ***
        };

        allUsers.Add(lecturer);
        await _users.SaveAsync(allUsers);

        TempData["RegistrationMessage"] =
            "Your lecturer account has been created and sent to HR for approval. " +
            "You will be able to log in once HR activates your profile.";

        return RedirectToAction(nameof(Login));
    }

    // ---------- ACCESS DENIED / LOGOUT ----------

    [HttpGet]
    public IActionResult Denied() => View();

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // ---------- Helpers ----------

    private async Task SignInAsync(AppUser user)
    {
        var claims = new List<SecClaim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name,  user.Name ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role,  user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    // Same hashing as JsonUserStore
    private string HashPassword(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
