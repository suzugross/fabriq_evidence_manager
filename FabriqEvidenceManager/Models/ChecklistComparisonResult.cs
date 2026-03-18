namespace FabriqEvidenceManager.Models;

/// <summary>
/// ベースラインPCとのチェックリスト照合結果。
/// OverallStatus の一致判定と VerifyItems 各項目の Status 照合を行う。
/// </summary>
public sealed class ChecklistComparisonResult
{
    /// <summary>ベースラインPCのチェックリスト全体判定 (例: "OK", "NG")</summary>
    public string BaselineOverallStatus { get; init; } = "";

    /// <summary>対象PCのチェックリスト全体判定</summary>
    public string ActualOverallStatus { get; init; } = "";

    /// <summary>OverallStatus が一致するか</summary>
    public bool OverallStatusMatch =>
        BaselineOverallStatus.Equals(ActualOverallStatus, StringComparison.OrdinalIgnoreCase);

    /// <summary>VerifyItems (PC Settings, Printer等) の各項目照合結果</summary>
    public List<VerificationItem> VerifyItemComparisons { get; init; } = [];

    /// <summary>全項目が一致するか (OverallStatus + VerifyItems)</summary>
    public bool AllMatch =>
        OverallStatusMatch
        && VerifyItemComparisons.All(i => i.Status == VerificationStatus.Match);

    /// <summary>不一致の件数 (OverallStatus不一致は1件としてカウント)</summary>
    public int MismatchCount =>
        (OverallStatusMatch ? 0 : 1)
        + VerifyItemComparisons.Count(i => i.Status != VerificationStatus.Match);
}
