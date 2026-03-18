namespace FabriqEvidenceManager.Models;

/// <summary>
/// ベースラインPCとのシステム基本情報 (OS名・バージョン・CPU・メモリ) 照合結果。
/// 各項目は既存の <see cref="VerificationItem"/> を再利用する。
/// </summary>
public sealed class SystemInfoComparisonResult
{
    /// <summary>照合結果の項目一覧 (OS名, バージョン, CPU, メモリ)</summary>
    public List<VerificationItem> Items { get; init; } = [];

    /// <summary>全項目が Match かどうか</summary>
    public bool AllMatch => Items.Count > 0 && Items.All(i => i.Status == VerificationStatus.Match);

    /// <summary>不一致の件数</summary>
    public int MismatchCount => Items.Count(i => i.Status != VerificationStatus.Match);
}
