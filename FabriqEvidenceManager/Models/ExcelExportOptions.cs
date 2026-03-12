using System.Collections.Generic;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// Excel エクスポート時の追加データオプション。
/// hostlist 突合結果・チェックリスト・実行履歴を渡すことで追加シートが生成される。
/// </summary>
public sealed class ExcelExportOptions
{
    /// <summary>PC名 → 突合レポート の辞書</summary>
    public IReadOnlyDictionary<string, VerificationReport>? VerificationReports { get; init; }

    /// <summary>PC名 → チェックリスト結果 の辞書</summary>
    public IReadOnlyDictionary<string, ChecklistResult>? ChecklistResults { get; init; }

    /// <summary>PC名 → 実行履歴一覧 の辞書</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ExportHistoryEntry>>? ExportHistories { get; init; }

    /// <summary>PC名 → ベースライン突合レポート の辞書</summary>
    public IReadOnlyDictionary<string, BaselineComparisonReport>? BaselineReports { get; init; }
}
