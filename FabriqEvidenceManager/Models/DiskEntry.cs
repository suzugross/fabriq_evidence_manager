namespace FabriqEvidenceManager.Models;

public sealed class DiskEntry
{
    public int Number { get; init; }
    public required string FriendlyName { get; init; }
    public string SerialNumber { get; init; } = string.Empty;
    public string SizeGB { get; init; } = string.Empty;
    public string PartitionStyle { get; init; } = string.Empty;
    public string HealthStatus { get; init; } = string.Empty;
    public string OperationalStatus { get; init; } = string.Empty;
}
