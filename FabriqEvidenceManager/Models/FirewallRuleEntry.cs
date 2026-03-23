namespace FabriqEvidenceManager.Models;

/// <summary>
/// 12_FirewallRules.csv から抽出されたWindowsファイアウォールルール。
/// </summary>
public sealed class FirewallRuleEntry
{
    public required string DisplayName { get; init; }
    public bool Enabled { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Profile { get; init; } = string.Empty;
}
