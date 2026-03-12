namespace FabriqEvidenceManager.Models;

/// <summary>
/// checklist HTML から抽出した全体の結果。
/// </summary>
public sealed class ChecklistResult
{
    /// <summary>全体判定 (例: "OK", "NG", "Partial")</summary>
    public string OverallStatus { get; init; } = string.Empty;

    /// <summary>設定突合結果 (verify-table から抽出)</summary>
    public List<ChecklistVerifyItem> VerifyItems { get; init; } = [];

    /// <summary>モジュール実行結果</summary>
    public List<ChecklistModuleItem> ModuleItems { get; init; } = [];

    /// <summary>サマリ: OK 件数</summary>
    public int OkCount => ModuleItems.Count(m =>
        m.Status.Equals("OK", StringComparison.OrdinalIgnoreCase)
        || m.Status.Equals("Success", StringComparison.OrdinalIgnoreCase));

    /// <summary>サマリ: NG 件数</summary>
    public int NgCount => ModuleItems.Count(m =>
        m.Status.Equals("NG", StringComparison.OrdinalIgnoreCase)
        || m.Status.Equals("Error", StringComparison.OrdinalIgnoreCase));

    /// <summary>サマリ: Skip 件数</summary>
    public int SkipCount => ModuleItems.Count(m =>
        m.Status.Equals("Skip", StringComparison.OrdinalIgnoreCase)
        || m.Status.Equals("Skipped", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// チェックリスト HTML の verify-table から抽出した1行。
/// </summary>
public sealed class ChecklistVerifyItem
{
    /// <summary>チェック項目名 (例: "PC Name", "Ethernet IP")</summary>
    public string ItemName { get; init; } = string.Empty;

    /// <summary>期待値 (hostlist)</summary>
    public string Expected { get; init; } = string.Empty;

    /// <summary>実測値 (OS)</summary>
    public string Actual { get; init; } = string.Empty;

    /// <summary>結果 (例: "Match", "Mismatch")</summary>
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// チェックリスト HTML のモジュール実行テーブルから抽出した1行。
/// </summary>
public sealed class ChecklistModuleItem
{
    /// <summary>実行順序番号</summary>
    public string Order { get; init; } = string.Empty;

    /// <summary>モジュール名</summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>カテゴリ</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>結果 (例: "OK", "NG", "Skip")</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>実行時刻</summary>
    public string Time { get; init; } = string.Empty;

    /// <summary>メッセージ</summary>
    public string Message { get; init; } = string.Empty;
}
