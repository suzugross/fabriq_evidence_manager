namespace FabriqEvidenceManager.Models;

/// <summary>
/// 05_DomainStatus.txt (dsregcmd /status) から抽出されたドメイン参加状態。
/// Sections にセクション別の全Key-Valueを保持し、主要フィールドは専用プロパティで公開する。
/// </summary>
public sealed class DomainStatusData
{
    // ---- ヘッダーセクション (WMI / .NET) ----
    public bool PartOfDomain { get; init; }
    public string Domain { get; init; } = string.Empty;
    public string DomainRole { get; init; } = string.Empty;
    public string CurrentUser { get; init; } = string.Empty;

    // ---- dsregcmd セクション ----
    public bool AzureAdJoined { get; init; }
    public bool DomainJoined { get; init; }
    public string DomainName { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;

    // ---- ドメインユーザー・グループ (ドメイン参加時のみ実データ) ----
    public List<string> DomainUsers { get; init; } = [];
    public List<string> DomainGroups { get; init; } = [];

    /// <summary>セクション名 → (Key → Value) の全量データ</summary>
    public Dictionary<string, Dictionary<string, string>> Sections { get; init; } = [];
}
