using ContractMonthlyClaimSystem.Models;
using ContractMonthlyClaimSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ContractMonthlyClaimSystem.Controllers;

[Authorize(Roles = "Manager")]
public class ManagerController : Controller
{
    private readonly IClaimStore _store;

    public ManagerController(IClaimStore store)
    {
        _store = store;
    }

    // === Verified claims waiting for manager approval ===
    public async Task<IActionResult> Index()
    {
        var all = await _store.GetAllAsync();
        var verified = all.Where(c => c.Status == ClaimStatus.Verified).ToList();

        ViewBag.VerifiedCount = verified.Count;
        ViewBag.TotalAwaiting = verified.Sum(c => c.Total);

        ViewBag.LastAction = HttpContext.Session.GetString("LastManagerAction");

        var ordered = verified
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

        ViewBag.LastAction = HttpContext.Session.GetString("LastManagerAction");
        ViewBag.SelectedStatus = status;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");

        return View(ordered);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var claim = await _store.GetAsync(id);
        if (claim is null) return NotFound();

        claim.Status = ClaimStatus.Approved;
        await _store.UpdateAsync(claim);

        HttpContext.Session.SetString(
            "LastManagerAction",
            $"Approved claim {id} at {DateTime.Now:g}");

        TempData["Success"] = "Claim approved.";
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
            "LastManagerAction",
            $"Rejected claim {id} at {DateTime.Now:g}");

        TempData["Success"] = "Claim rejected.";
        return RedirectToAction(nameof(Index));
    }
}
