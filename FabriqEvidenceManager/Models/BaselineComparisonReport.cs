namespace FabriqEvidenceManager.Models;

/// <summary>
/// ベースライン (パイロット実行) との突合結果1項目。
/// ModuleName をキーに Status の一致/不一致を判定する。
/// </summary>
public sealed class BaselineComparisonItem
{
    /// <summary>モジュール名</summary>
    public required string ModuleName { get; init; }

    /// <summary>カテゴリ</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>ベースライン側のステータス (期待値)</summary>
    public string ExpectedStatus { get; init; } = string.Empty;

    /// <summary>対象PC側のステータス (実測値)</summary>
    public string ActualStatus { get; init; } = string.Empty;

    /// <summary>突合判定</summary>
    public BaselineMatchStatus MatchStatus { get; init; }
}

/// <summary>
/// ベースライン突合のステータス。
/// </summary>
public enum BaselineMatchStatus
{
    /// <summary>ステータス一致</summary>
    Match,

    /// <summary>ステータス不一致</summary>
    Mismatch,

    /// <summary>ベースラインにはあるが対象PCに該当モジュールが存在しない</summary>
    MissingInActual,

    /// <summary>対象PCにはあるがベースラインに該当モジュールが存在しない</summary>
    ExtraInActual,
}

/// <summary>
/// 1台のPCに対するベースライン突合結果全体。
/// 実行サマリ (Items) に加えて、SystemInfo・チェックリストの照合結果を含む。
/// </summary>
public sealed class BaselineComparisonReport
{
    /// <summary>対象PC名</summary>
    public required string PcName { get; init; }

    /// <summary>実行サマリ突合結果の項目一覧 (ModuleName × Status)</summary>
    public List<BaselineComparisonItem> Items { get; init; } = [];

    /// <summary>システム基本情報の照合結果 (OS名・バージョン・CPU・メモリ)</summary>
    public SystemInfoComparisonResult? SystemInfoComparison { get; init; }

    /// <summary>チェックリストの照合結果 (OverallStatus + VerifyItems)</summary>
    public ChecklistComparisonResult? ChecklistComparison { get; init; }

    /// <summary>実行サマリが全項目 Match かどうか</summary>
    public bool AllMatch => Items.Count > 0 && Items.All(i => i.MatchStatus == BaselineMatchStatus.Match);

    /// <summary>全カテゴリの不一致件数合計</summary>
    public int DifferenceCount =>
        Items.Count(i => i.MatchStatus != BaselineMatchStatus.Match)
        + (SystemInfoComparison?.MismatchCount ?? 0)
        + (ChecklistComparison?.MismatchCount ?? 0);

    /// <summary>全カテゴリが完全一致かどうか</summary>
    public bool AllCategoriesMatch =>
        AllMatch
        && (SystemInfoComparison?.AllMatch ?? true)
        && (ChecklistComparison?.AllMatch ?? true);
}
