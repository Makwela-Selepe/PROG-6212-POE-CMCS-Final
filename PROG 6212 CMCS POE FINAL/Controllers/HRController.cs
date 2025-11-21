using ContractMonthlyClaimSystem.Models;
using ContractMonthlyClaimSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace ContractMonthlyClaimSystem.Controllers;

[Authorize(Roles = "HR")]
public class HrController : Controller
{
    private readonly IUserStore _users;
    private readonly IClaimStore _claims;

    public HrController(IUserStore users, IClaimStore claims)
    {
        _users = users;
        _claims = claims;
    }

    // === HR Dashboard summary ===
    public async Task<IActionResult> Index()
    {
        var allClaims = await _claims.GetAllAsync();
        var allUsers = await _users.GetAllAsync();

        ViewBag.TotalUsers = allUsers.Count;
        ViewBag.TotalClaims = allClaims.Count;
        ViewBag.Approved = allClaims.Count(c => c.Status == ClaimStatus.Approved);
        ViewBag.Rejected = allClaims.Count(c => c.Status == ClaimStatus.Rejected);

        return View();
    }

    // === Manage Users (list) ===
    public async Task<IActionResult> ManageUsers()
    {
        var users = await _users.GetAllAsync();
        return View(users);
    }

    // --- Create Lecturer ---
    // --- Create Lecturer ---
    [HttpGet]
    public IActionResult CreateLecturer()
    {
        // default values for the form
        return View(new AppUser { Role = UserRole.Lecturer, HourlyRate = 350m });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLecturer(AppUser model, string password)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // create the lecturer using the user store (handles hashing etc.)
        var created = await _users.CreateLecturerAsync(
            model.Name ?? string.Empty,
            model.Email ?? string.Empty,
            password);

        if (created == null)
        {
            ModelState.AddModelError(string.Empty, "A user with that email already exists.");
            return View(model);
        }

        // update HourlyRate + mark as approved
        var users = await _users.GetAllAsync();
        var stored = users.First(u => u.Id == created.Id);

        stored.HourlyRate = model.HourlyRate;
        stored.IsApproved = true;   // HR-created lecturers are active immediately

        await _users.SaveAsync(users);

        TempData["HrMessage"] = "Lecturer created and activated.";
        return RedirectToAction(nameof(ManageUsers));
    }


    // --- Edit User ---
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var users = await _users.GetAllAsync();
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user == null) return NotFound();

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AppUser model)
    {
        var users = await _users.GetAllAsync();
        var user = users.FirstOrDefault(u => u.Id == model.Id);
        if (user == null) return NotFound();

        user.Name = model.Name;
        user.Email = model.Email;
        user.Role = model.Role;
        user.HourlyRate = model.HourlyRate;

        await _users.SaveAsync(users);

        TempData["Success"] = "User details updated.";
        return RedirectToAction(nameof(ManageUsers));
    }

    // === HR Approved Claims summary (for invoices / reports) ===
    public async Task<IActionResult> Reports()
    {
        var claims = await _claims.GetAllAsync();
        var approved = claims.Where(c => c.Status == ClaimStatus.Approved);

        var rows = approved
            .GroupBy(c => new { c.LecturerName, c.Email })
            .Select(g => new HrReportRow
            {
                LecturerName = g.Key.LecturerName,
                Email = g.Key.Email,
                TotalHours = g.Sum(c => c.HoursWorked),
                TotalAmount = g.Sum(c => c.Total)
            })
            .OrderByDescending(r => r.TotalAmount)
            .ToList();

        return View(rows);
    }
    public async Task<IActionResult> ExportReportsPdf()
    {
        // 1) Get approved claims and group per lecturer
        var claims = await _claims.GetAllAsync();
        var approved = claims.Where(c => c.Status == ClaimStatus.Approved);

        var rows = approved
            .GroupBy(c => new { c.LecturerName, c.Email })
            .Select(g => new HrReportRow
            {
                LecturerName = g.Key.LecturerName,
                Email = g.Key.Email,
                TotalHours = g.Sum(c => c.HoursWorked),
                TotalAmount = g.Sum(c => c.Total)
            })
            .OrderByDescending(r => r.TotalAmount)
            .ToList();

        // 2) Prepare PDF in memory
        using var ms = new MemoryStream();
        var document = new Document(PageSize.A4, 36, 36, 36, 36);
        PdfWriter.GetInstance(document, ms);

        document.Open();

        // Title
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
        var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

        document.Add(new Paragraph("Approved Claims Report", titleFont));
        document.Add(new Paragraph($"Generated: {DateTime.Now:g}", normalFont));
        document.Add(Chunk.Newline);

        // 3) Table with lecturer totals
        var table = new PdfPTable(4) { WidthPercentage = 100 };
        table.SetWidths(new float[] { 3f, 4f, 2f, 2f });

        void AddCell(string text, bool header = false)
        {
            var font = header
                ? FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)
                : normalFont;

            var cell = new PdfPCell(new Phrase(text ?? string.Empty, font))
            {
                HorizontalAlignment = Element.ALIGN_LEFT,
                Padding = 4f,
                BackgroundColor = header ? new BaseColor(230, 230, 230) : BaseColor.White
            };
            table.AddCell(cell);
        }

        AddCell("Lecturer", true);
        AddCell("Email", true);
        AddCell("Total Hours", true);
        AddCell("Total Amount", true);

        var grandHours = 0;
        var grandAmount = 0m;

        foreach (var r in rows)
        {
            AddCell(r.LecturerName);
            AddCell(r.Email);
            AddCell(r.TotalHours.ToString());
            AddCell(r.TotalAmount.ToString("C"));

            grandHours += r.TotalHours;
            grandAmount += r.TotalAmount;
        }

        // Grand total row
        AddCell("TOTAL", true);
        AddCell(string.Empty, true);
        AddCell(grandHours.ToString(), true);
        AddCell(grandAmount.ToString("C"), true);

        document.Add(table);

        document.Close();

        var bytes = ms.ToArray();
        return File(bytes, "application/pdf", "ApprovedClaimsReport.pdf");
    }
    [HttpPost]
    [Authorize(Roles = "HR")]
    public async Task<IActionResult> ApproveLecturer(Guid id)
    {
        var users = await _users.GetAllAsync();
        var user = users.FirstOrDefault(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        user.IsApproved = true;
        await _users.SaveAsync(users);

        TempData["HrMessage"] = $"{user.Name} has been approved and can now log in.";

        return RedirectToAction(nameof(ManageUsers));
    }

    // === HR – All claims history with statuses & downloads ===
    public async Task<IActionResult> AllClaims(ClaimStatus? status, DateTime? from, DateTime? to)
    {
        var claims = await _claims.GetAllAsync();

        if (status.HasValue)
            claims = claims.Where(c => c.Status == status.Value).ToList();

        if (from.HasValue)
            claims = claims.Where(c => c.CreatedUtc.Date >= from.Value.Date).ToList();

        if (to.HasValue)
            claims = claims.Where(c => c.CreatedUtc.Date <= to.Value.Date).ToList();

        var ordered = claims
            .OrderByDescending(c => c.CreatedUtc)
            .ToList();

        ViewBag.SelectedStatus = status;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");

        return View(ordered);
    }


    // DTO for report rows
    public class HrReportRow
    {
        public string LecturerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalHours { get; set; }
        public decimal TotalAmount { get; set; }
    }
    public async Task<IActionResult> ExportUsers()
    {
        var users = await _users.GetAllAsync();
        var lecturers = users.Where(u => u.Role == UserRole.Lecturer).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Name,Email,Role,HourlyRate");

        foreach (var u in lecturers)
        {
            string name = (u.Name ?? "").Replace("\"", "\"\"");
            string email = (u.Email ?? "").Replace("\"", "\"\"");

            sb.AppendLine($"\"{name}\",\"{email}\",{u.Role},{u.HourlyRate}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "lecturers.csv");
    }
}
