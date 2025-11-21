using ContractMonthlyClaimSystem.Models;
using ContractMonthlyClaimSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ContractMonthlyClaimSystem.Controllers;

[Authorize(Roles = "Coordinator")]
public class CoordinatorController : Controller
{
    private readonly IClaimStore _store;

    public CoordinatorController(IClaimStore store)
    {
        _store = store;
    }

    // === Pending claims waiting for coordinator verification ===
    public async Task<IActionResult> Index()
    {
        var all = await _store.GetAllAsync();
        var pending = all.Where(c => c.Status == ClaimStatus.Pending).ToList();

        ViewBag.PendingCount = pending.Count;
        ViewBag.RejectedCount = all.Count(c => c.Status == ClaimStatus.Rejected);
        ViewBag.OldestPending = pending.Any()
            ? pending.Min(c => c.CreatedUtc).ToLocalTime()
            : (DateTime?)null;

        ViewBag.LastAction = HttpContext.Session.GetString("LastCoordinatorAction");

        var ordered = pending
            .OrderByDescending(c => c.CreatedUtc)
            .ToList();

        return View(ordered);
    }


    // === History of all claims with their final status ===
    public async Task<IActionResult> History(ClaimStatus? status, DateTime? from, DateTime? to)
    {
        var claims = await _store.GetAllAsync();

        if (status.HasValue)
            claims = claims.Where(c => c.Status == status.Value).ToList();

        if (from.HasValue)
            claims = claims.Where(c => c.CreatedUtc.Date >= from.Value.Date).ToList();

        if (to.HasValue)
            claims = claims.Where(c => c.CreatedUtc.Date <= to.Value.Date).ToList();

        var ordered = claims
            .OrderByDescending(c => c.CreatedUtc)
            .ToList();

        ViewBag.LastAction = HttpContext.Session.GetString("LastCoordinatorAction");
        ViewBag.SelectedStatus = status;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");

        return View(ordered);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(Guid id)
    {
        var claim = await _store.GetAsync(id);
        if (claim is null) return NotFound();

        claim.Status = ClaimStatus.Verified;
        await _store.UpdateAsync(claim);

        HttpContext.Session.SetString(
            "LastCoordinatorAction",
            $"Verified claim {id} at {DateTime.Now:g}");

        TempData["Success"] = "Claim verified.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id)
    {
        var claim = await _store.GetAsync(id);
        if (claim is null) return NotFound();

        claim.Status = ClaimStatus.Rejected;
        await _store.UpdateAsync(claim);

        HttpContext.Session.SetString(
            "LastCoordinatorAction",
            $"Rejected claim {id} at {DateTime.Now:g}");

        TempData["Success"] = "Claim rejected.";
        return RedirectToAction(nameof(Index));
    }
}
