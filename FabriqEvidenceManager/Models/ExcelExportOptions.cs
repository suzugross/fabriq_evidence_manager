using System.Collections.Generic;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// Excel エクスポート時の追加データオプション。
/// hostlist 突合結果・チェックリスト・実行履歴を渡すことで追加シートが生成される。
/// </summary>
public sealed class ExcelExportOptions
{
    // 注意: キーは文字列 PcName ではなく PcEvidence インスタンス参照。
    // 同じ計画 PC 名 (selectedNewPcName) を持つ複数 PC が衝突して
    // レポートが上書きされるバグ (v3.4.0 まで) を修正するため、参照ベースのキーに変更。

    /// <summary>PcEvidence → 突合レポート の辞書 (キーは参照同一性)</summary>
    public IReadOnlyDictionary<PcEvidence, VerificationReport>? VerificationReports { get; init; }

    /// <summary>PcEvidence → チェックリスト結果 の辞書 (キーは参照同一性)</summary>
    public IReadOnlyDictionary<PcEvidence, ChecklistResult>? ChecklistResults { get; init; }

    /// <summary>PcEvidence → 実行履歴一覧 の辞書 (キーは参照同一性)</summary>
    public IReadOnlyDictionary<PcEvidence, IReadOnlyList<ExportHistoryEntry>>? ExportHistories { get; init; }

    /// <summary>PcEvidence → ベースライン突合レポート の辞書 (キーは参照同一性)</summary>
    public IReadOnlyDictionary<PcEvidence, BaselineComparisonReport>? BaselineReports { get; init; }

    /// <summary>
    /// Sheet 1 (PC情報一覧) 冒頭 6 行に出力する案件メタデータ (M6)。
    /// null の場合は従来通りヘッダブロックなしで列ヘッダから開始する。
    /// </summary>
    public ExcelDeliveryMetadata? DeliveryMetadata { get; init; }
}
