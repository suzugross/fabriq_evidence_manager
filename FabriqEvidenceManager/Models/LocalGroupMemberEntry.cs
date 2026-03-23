namespace FabriqEvidenceManager.Models;

/// <summary>
/// 04_LocalGroupMembers.csv から抽出されたグループメンバーシップ情報。
/// </summary>
public sealed class LocalGroupMemberEntry
{
    public required string GroupName { get; init; }
    public required string MemberName { get; init; }
    public string ObjectClass { get; init; } = string.Empty;
    public string PrincipalSource { get; init; } = string.Empty;
}
