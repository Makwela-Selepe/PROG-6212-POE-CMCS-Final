namespace ContractMonthlyClaimSystem.Models;

public class UploadMeta
{
    // Primary key for EF
    public int Id { get; set; }

    // Foreign key to the Claim (optional but nice)
    public Guid ClaimId { get; set; }

    public string FileName { get; set; } = string.Empty;   // original filename
    public string SavedAs { get; set; } = string.Empty;   // file on disk
    public long Size { get; set; }                   // bytes
}
