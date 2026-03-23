namespace FabriqEvidenceManager.Models;

/// <summary>
/// 02_LocalUsers.csv から抽出されたローカルユーザーアカウント情報。
/// </summary>
public sealed class LocalUserEntry
{
    public required string Name { get; init; }
    public bool Enabled { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SID { get; init; } = string.Empty;
    public string LastLogon { get; init; } = string.Empty;
    public string PasswordLastSet { get; init; } = string.Empty;
    public bool PasswordRequired { get; init; }
    public string PrincipalSource { get; init; } = string.Empty;
}
