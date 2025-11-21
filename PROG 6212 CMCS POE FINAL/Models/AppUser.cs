namespace ContractMonthlyClaimSystem.Models;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }

    // NEW
    public decimal HourlyRate { get; set; } = 0m;

    // NEW – controls login permission
    public bool IsApproved { get; set; } = false;
}
