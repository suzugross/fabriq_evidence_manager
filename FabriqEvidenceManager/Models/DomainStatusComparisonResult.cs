namespace FabriqEvidenceManager.Models;

/// <summary>
/// ベースライン PC とのドメイン参加状態の照合結果。
/// PartOfDomain / Domain / DomainRole / AzureAdJoined / DomainJoined /
/// DomainName / TenantName の 7 項目を比較する (CurrentUser は PC 個別なので除外)。
/// </summary>
public sealed class DomainStatusComparisonResult
{
    public List<VerificationItem> Items { get; init; } = [];

    public bool AllMatch => Items.Count > 0 && Items.All(i => i.Status == VerificationStatus.Match);

    public int MismatchCount => Items.Count(i => i.Status != VerificationStatus.Match);
}
