namespace FabriqEvidenceManager.Models;

public sealed class WindowsUpdateEntry
{
    public required string HotFixID { get; init; }
    public string Description { get; init; } = string.Empty;
    public string InstalledBy { get; init; } = string.Empty;
    public string InstalledOn { get; init; } = string.Empty;
}
