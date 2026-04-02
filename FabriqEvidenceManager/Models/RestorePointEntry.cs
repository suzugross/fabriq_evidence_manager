namespace FabriqEvidenceManager.Models;

public sealed class RestorePointEntry
{
    public int SequenceNumber { get; init; }
    public required string Description { get; init; }
    public string RestorePointType { get; init; } = string.Empty;
    public string CreationTime { get; init; } = string.Empty;
}
