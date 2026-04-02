namespace FabriqEvidenceManager.Models;

public sealed class UserProfileEntry
{
    public required string LocalPath { get; init; }
    public string SID { get; init; } = string.Empty;
    public string LastUseTime { get; init; } = string.Empty;
    public bool Loaded { get; init; }
}
