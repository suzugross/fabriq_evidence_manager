namespace FabriqEvidenceManager.Models;

/// <summary>
/// 12_FirewallProfiles.csv から抽出されたWindowsファイアウォールプロファイル設定。
/// Domain / Private / Public の3プロファイル。
/// </summary>
public sealed class FirewallProfileEntry
{
    public required string Name { get; init; }
    public bool Enabled { get; init; }
    public string DefaultInboundAction { get; init; } = string.Empty;
    public string DefaultOutboundAction { get; init; } = string.Empty;
    public string LogFileName { get; init; } = string.Empty;
}
