namespace FabriqEvidenceManager.Models;

/// <summary>
/// ベースライン PC との Windows / Office ライセンス情報の照合結果。
/// 各項目は <see cref="VerificationItem"/> として ItemName=フィールド名 (例: "Windows: ライセンスファミリ") /
/// Expected=baseline / Actual=対象PC の組で表す。
/// 配列単一で Win + Office を含む (ItemName のプレフィックスで区別)。
/// </summary>
public sealed class LicenseComparisonResult
{
    /// <summary>照合結果の項目一覧 (Win 4 項目 + Office 4 項目、データがある側のみ)</summary>
    public List<VerificationItem> Items { get; init; } = [];

    /// <summary>全項目が Match かどうか</summary>
    public bool AllMatch => Items.Count > 0 && Items.All(i => i.Status == VerificationStatus.Match);

    /// <summary>不一致の件数</summary>
    public int MismatchCount => Items.Count(i => i.Status != VerificationStatus.Match);
}
