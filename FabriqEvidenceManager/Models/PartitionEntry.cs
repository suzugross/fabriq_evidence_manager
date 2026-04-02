namespace FabriqEvidenceManager.Models;

public sealed class PartitionEntry
{
    public int DiskNumber { get; init; }
    public int PartitionNumber { get; init; }
    public string DriveLetter { get; init; } = string.Empty;
    public string SizeGB { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public bool IsBoot { get; init; }
    public bool IsActive { get; init; }
}
