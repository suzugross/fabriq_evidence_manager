using System.Collections.Generic;
using System.Linq;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Baseline;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// パイロット実行のベースライン管理 (薄いオーケストレータ)。
///
/// 実装は <see cref="IBaselineComparator"/> プラグインに分離されており、
/// LoadFromPc / CompareAll は登録済みコンパレータを順次呼び出すだけ。
/// 新カテゴリを追加するには <see cref="IBaselineComparator"/> 実装を 1 つ追加し
/// DI 登録するのみ (Phase 1: Plugin 化リファクタ)。
/// </summary>
public sealed class BaselineService : IBaselineService
{
    private readonly IReadOnlyList<IBaselineComparator> _comparators;

    /// <inheritdoc />
    public string? BaselinePcName { get; private set; }

    public BaselineService(IEnumerable<IBaselineComparator> comparators)
    {
        _comparators = comparators.ToList();
    }

    /// <inheritdoc />
    public bool IsLoaded => _comparators.Any(c => c.HasBaselineData);

    /// <inheritdoc />
    public int Load(string csvFilePath)
    {
        // 実行サマリ単体 CSV からロード (backward compat 経路)
        var executor = _comparators.OfType<ExecutionSummaryComparator>().FirstOrDefault();
        return executor?.LoadFromCsv(csvFilePath) ?? 0;
    }

    /// <inheritdoc />
    public void LoadFromPc(PcEvidence baselinePc)
    {
        BaselinePcName = baselinePc.PcName;
        foreach (var comparator in _comparators)
        {
            comparator.CacheBaseline(baselinePc);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        BaselinePcName = null;
        foreach (var comparator in _comparators)
        {
            comparator.ClearBaseline();
        }
    }

    /// <inheritdoc />
    public BaselineComparisonReport Compare(string pcName, IReadOnlyList<ExportHistoryEntry> actualHistory)
    {
        // 実行サマリのみ history 注入で照合する backward compat 経路
        var executor = _comparators.OfType<ExecutionSummaryComparator>().FirstOrDefault();
        var items = executor?.CompareWithHistory(actualHistory) ?? [];
        return new BaselineComparisonReport
        {
            PcName = pcName,
            Items = items,
        };
    }

    /// <inheritdoc />
    public BaselineComparisonReport CompareAll(PcEvidence targetPc)
    {
        var report = new BaselineComparisonReport { PcName = targetPc.PcName };
        foreach (var comparator in _comparators)
        {
            comparator.Compare(report, targetPc);
        }
        return report;
    }
}
