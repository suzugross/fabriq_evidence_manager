using System;
using System.Collections.Generic;
using System.IO;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// 実行サマリ (export_history.csv) のベースライン照合。
/// ModuleName をキーに Status の一致/不一致を判定し、
/// <see cref="BaselineComparisonReport.Items"/> に <see cref="BaselineComparisonItem"/> を追加する。
/// </summary>
public sealed class ExecutionSummaryComparator : IBaselineComparator
{
    private readonly IChecklistParserService _checklistParser;

    private Dictionary<string, ExportHistoryEntry> _baselineMap = new(StringComparer.OrdinalIgnoreCase);
    private List<ExportHistoryEntry> _baselineEntries = [];

    public string CategoryId => "ExecutionSummary";
    public string DisplayName => "実行サマリ";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baselineEntries.Count > 0;

    public ExecutionSummaryComparator(IChecklistParserService checklistParser)
    {
        _checklistParser = checklistParser;
    }

    public void CacheBaseline(PcEvidence baselinePc)
    {
        // 既存挙動を保持: CSV 不在時は前回キャッシュをクリアせず保持。
        // (ベースライン PC の switch 時に CSV だけ無いケースの挙動互換)
        if (baselinePc.ExportHistoryFilePath is null || !File.Exists(baselinePc.ExportHistoryFilePath))
            return;
        LoadFromCsv(baselinePc.ExportHistoryFilePath);
    }

    /// <summary>
    /// CSV ファイル単体からベースラインを読み込む。
    /// <see cref="BaselineService.Load(string)"/> backward compat 用に公開メソッドとする。
    /// </summary>
    public int LoadFromCsv(string csvFilePath)
    {
        var entries = _checklistParser.ParseExportHistory(csvFilePath);
        var map = new Dictionary<string, ExportHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            // 同一モジュール名が複数回出現する場合は最後勝ち (既存挙動)
            map[entry.ModuleName] = entry;
        }
        _baselineEntries = [.. entries];
        _baselineMap = map;
        return map.Count;
    }

    public void ClearBaseline()
    {
        _baselineMap = new(StringComparer.OrdinalIgnoreCase);
        _baselineEntries = [];
    }

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (!HasBaselineData) return;
        if (targetPc.ExportHistoryFilePath is null || !File.Exists(targetPc.ExportHistoryFilePath)) return;

        var actualHistory = _checklistParser.ParseExportHistory(targetPc.ExportHistoryFilePath);
        report.Items.AddRange(CompareWithHistory(actualHistory));
    }

    /// <summary>
    /// 履歴エントリ列を直接受け取って比較する。
    /// <see cref="BaselineService.Compare(string, IReadOnlyList{ExportHistoryEntry})"/> backward compat 用。
    /// </summary>
    public List<BaselineComparisonItem> CompareWithHistory(IReadOnlyList<ExportHistoryEntry> actualHistory)
    {
        var items = new List<BaselineComparisonItem>();

        var actualMap = new Dictionary<string, ExportHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in actualHistory)
            actualMap[entry.ModuleName] = entry;

        // 1. ベースライン側を基準に比較
        foreach (var baseline in _baselineMap.Values)
        {
            if (actualMap.TryGetValue(baseline.ModuleName, out var actual))
            {
                items.Add(new BaselineComparisonItem
                {
                    ModuleName = baseline.ModuleName,
                    Category = baseline.Category,
                    ExpectedStatus = baseline.Status,
                    ActualStatus = actual.Status,
                    MatchStatus = baseline.Status.Equals(actual.Status, StringComparison.OrdinalIgnoreCase)
                        ? BaselineMatchStatus.Match
                        : BaselineMatchStatus.Mismatch,
                });
            }
            else
            {
                items.Add(new BaselineComparisonItem
                {
                    ModuleName = baseline.ModuleName,
                    Category = baseline.Category,
                    ExpectedStatus = baseline.Status,
                    ActualStatus = "(未実行)",
                    MatchStatus = BaselineMatchStatus.MissingInActual,
                });
            }
        }

        // 2. 実測側のみ存在するモジュール
        foreach (var actual in actualMap.Values)
        {
            if (!_baselineMap.ContainsKey(actual.ModuleName))
            {
                items.Add(new BaselineComparisonItem
                {
                    ModuleName = actual.ModuleName,
                    Category = actual.Category,
                    ExpectedStatus = "(ベースラインなし)",
                    ActualStatus = actual.Status,
                    MatchStatus = BaselineMatchStatus.ExtraInActual,
                });
            }
        }

        return items;
    }
}
