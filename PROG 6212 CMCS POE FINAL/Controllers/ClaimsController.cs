using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ContractMonthlyClaimSystem.Services;
using ContractMonthlyClaimSystem.Models;
// avoid collision with System.Security.Claims.Claim
using ClaimModel = ContractMonthlyClaimSystem.Models.Claim;

namespace ContractMonthlyClaimSystem.Controllers;

[Authorize] // all actions require login
public class ClaimsController : Controller
{
    private readonly IClaimStore _store;
    private readonly IWebHostEnvironment _env;
    private readonly IFileGuard _guard;
    private readonly IUserStore _users;

    public ClaimsController(
        IClaimStore store,
        IWebHostEnvironment env,
        IFileGuard guard,
        IUserStore users)
    {
        _store = store;
        _env = env;
        _guard = guard;
        _users = users;
    }

    // =========================================================
    // Lecturer creates a new claim
    // =========================================================
    [Authorize(Roles = "Lecturer")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // Get current lecturer from claims (email)
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return Challenge();

        var user = await _users.FindByEmailAsync(email);
        if (user == null)
            return Forbid();

        var model = new ClaimModel
        {
            LecturerName = user.Name ?? string.Empty,
            Email = user.Email ?? string.Empty,
            LecturerEmail = user.Email ?? string.Empty,
            HourlyRate = user.HourlyRate
        };

        return View(model);
    }

    [Authorize(Roles = "Lecturer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClaimModel model, List<IFormFile>? files)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return Challenge();

        var user = await _users.FindByEmailAsync(email);
        if (user == null)
            return Forbid();

        // Always override these with values from the logged-in lecturer
        model.LecturerName = user.Name ?? string.Empty;
        model.Email = user.Email ?? string.Empty;
        model.LecturerEmail = user.Email ?? string.Empty;
        model.HourlyRate = user.HourlyRate;
        model.CreatedUtc = DateTime.UtcNow;
        model.Status = ClaimStatus.Pending;

        // Remove from ModelState so old 0.00 / empty values don't break validation
        ModelState.Remove(nameof(ClaimModel.LecturerName));
        ModelState.Remove(nameof(ClaimModel.Email));
        ModelState.Remove(nameof(ClaimModel.LecturerEmail));
        ModelState.Remove(nameof(ClaimModel.HourlyRate));

        if (!ModelState.IsValid)
            return View(model);

        // --------- Handle file uploads ----------
        if (files is { Count: > 0 })
        {
            var uploadRoot = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadRoot);

            foreach (var f in files)
            {
                if (f.Length == 0) continue;

                if (!_guard.IsAllowed(f.FileName, f.Length, out var err))
                {
                    ModelState.AddModelError("", $"{f.FileName}: {err}");
                    return View(model);
                }

                var unique = $"{Guid.NewGuid():N}{Path.GetExtension(f.FileName)}";
                var savePath = Path.Combine(uploadRoot, unique);

                using (var fs = System.IO.File.Create(savePath))
                    await f.CopyToAsync(fs);

                model.Uploads.Add(new UploadMeta
                {
                    FileName = f.FileName,
                    SavedAs = unique,
                    Size = f.Length
                });
            }
        }

        await _store.AddAsync(model);

        TempData["ok"] = "Claim submitted.";
        return RedirectToAction(nameof(Thanks), new { id = model.Id });
    }

    // =========================================================
    // After submit – lecturer sees a single claim
    // =========================================================
    [Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Thanks(Guid id)
    {
        var claim = await _store.GetAsync(id);
        return View(claim);
    }

    // =========================================================
    // Download an uploaded document for a claim
    // Lecturer + Coordinator + Manager + HR can all use this.
    // =========================================================
    [Authorize(Roles = "Lecturer,Coordinator,Manager,HR")]
    public async Task<IActionResult> Download(Guid claimId, string savedAs)
    {
        if (string.IsNullOrWhiteSpace(savedAs))
            return NotFound();

        var claim = await _store.GetAsync(claimId);
        if (claim == null)
            return NotFound();

        var upload = claim.Uploads.FirstOrDefault(u => u.SavedAs == savedAs);
        if (upload == null)
            return NotFound();

        var uploadRoot = Path.Combine(_env.WebRootPath, "uploads");
        var filePath = Path.Combine(uploadRoot, upload.SavedAs);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        const string contentType = "application/octet-stream";

        return PhysicalFile(filePath, contentType, fileDownloadName: upload.FileName);
    }
}
