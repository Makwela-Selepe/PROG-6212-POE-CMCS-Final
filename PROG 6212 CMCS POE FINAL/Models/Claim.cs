using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContractMonthlyClaimSystem.Models;

public class Claim
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(80, ErrorMessage = "Name cannot be longer than 80 characters.")]
    public string LecturerName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(100)]
    public string Email { get; set; } = "";

    [Range(1, 180, ErrorMessage = "Policy allows a maximum of 180 hours per month.")]
    public int HoursWorked { get; set; }

    [Range(50, 2000, ErrorMessage = "Hourly rate must be between 50 and 2000.")]
    public decimal HourlyRate { get; set; }

    [StringLength(250, ErrorMessage = "Notes cannot be longer than 250 characters.")]
    public string? Notes { get; set; }

    [NotMapped]
    public decimal Total => HoursWorked * HourlyRate;

    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<UploadMeta> Uploads { get; set; } = new();

    public string LecturerEmail { get; set; } = "";
}
