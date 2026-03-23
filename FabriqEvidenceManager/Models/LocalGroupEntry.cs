namespace FabriqEvidenceManager.Models;

/// <summary>
/// 03_LocalGroups.csv から抽出されたローカルグループ情報。
/// </summary>
public sealed class LocalGroupEntry
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string SID { get; init; } = string.Empty;
}
