using System.Security.Claims;
using ContractMonthlyClaimSystem.Models;
using ContractMonthlyClaimSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContractMonthlyClaimSystem.Controllers;

[Authorize(Roles = "Lecturer")]
public class LecturerController : Controller
{
    private readonly IClaimStore _store;

    public LecturerController(IClaimStore store)
    {
        _store = store;
    }

    public async Task<IActionResult> MyClaims()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var all = await _store.GetAllAsync();
        var mine = all
            .Where(c => c.LecturerEmail == email || c.Email == email)
            .OrderByDescending(c => c.CreatedUtc)
            .ToList();

        var pending = mine.Count(c => c.Status == ClaimStatus.Pending);
        var approved = mine.Where(c => c.Status == ClaimStatus.Approved).ToList();
        var totalPaid = approved.Sum(c => c.Total);

        ViewBag.PendingCount = pending;
        ViewBag.ApprovedCount = approved.Count;
        ViewBag.TotalApproved = totalPaid;

        return View(mine);
    }
}
