namespace FabriqEvidenceManager.Models;

/// <summary>
/// hostlist (Expected) と evidence (Actual) の突合結果1項目。
/// </summary>
public sealed class VerificationItem
{
    /// <summary>チェック項目名 (例: "ホスト名", "Ethernet IP")</summary>
    public required string ItemName { get; init; }

    /// <summary>期待値 (hostlist)</summary>
    public string Expected { get; init; } = string.Empty;

    /// <summary>実測値 (evidence)</summary>
    public string Actual { get; init; } = string.Empty;

    /// <summary>突合ステータス</summary>
    public VerificationStatus Status { get; init; }
}

/// <summary>
/// 突合ステータス。
/// </summary>
public enum VerificationStatus
{
    /// <summary>一致</summary>
    Match,

    /// <summary>不一致</summary>
    Mismatch,

    /// <summary>期待値なし (hostlist に値が未設定)</summary>
    NoExpected,

    /// <summary>実測値なし (evidence にデータがない)</summary>
    NoActual,
}

/// <summary>
/// 1台のPCに対する突合結果全体。
/// </summary>
public sealed class VerificationReport
{
    /// <summary>対象PC名</summary>
    public required string PcName { get; init; }

    /// <summary>hostlist のエントリが見つかったかどうか</summary>
    public bool HostlistFound { get; init; }

    /// <summary>突合結果の項目一覧</summary>
    public List<VerificationItem> Items { get; init; } = [];

    /// <summary>全項目が Match かどうか</summary>
    public bool AllMatch => Items.Count > 0 && Items.All(i => i.Status == VerificationStatus.Match);

    /// <summary>Mismatch の件数</summary>
    public int MismatchCount => Items.Count(i => i.Status == VerificationStatus.Mismatch);
}
