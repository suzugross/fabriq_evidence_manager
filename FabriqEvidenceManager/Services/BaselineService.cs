using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// パイロット実行のベースライン管理。
/// export_history CSV を読み込み、ModuleName をキーにした辞書を保持する。
/// 突合時はステータスの文字列完全一致で判定する。
/// </summary>
public sealed class BaselineService : IBaselineService
{
    private readonly IChecklistParserService _checklistParser;

    /// <summary>ModuleName → ExportHistoryEntry (ベースライン側)</summary>
    private Dictionary<string, ExportHistoryEntry> _baselineMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>ベースラインの全エントリ (順序保持)</summary>
    private List<ExportHistoryEntry> _baselineEntries = [];

    public BaselineService(IChecklistParserService checklistParser)
    {
        _checklistParser = checklistParser;
    }

    /// <inheritdoc />
    public bool IsLoaded => _baselineEntries.Count > 0;

    /// <inheritdoc />
    public int Load(string csvFilePath)
    {
        var entries = _checklistParser.ParseExportHistory(csvFilePath);

        var map = new Dictionary<string, ExportHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            // 同一モジュール名が複数回出現する場合は最後のエントリを採用
            map[entry.ModuleName] = entry;
        }

        _baselineEntries = [.. entries];
        _baselineMap = map;

        return map.Count;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _baselineEntries.Clear();
        _baselineMap.Clear();
    }

    /// <inheritdoc />
    public BaselineComparisonReport Compare(string pcName, IReadOnlyList<ExportHistoryEntry> actualHistory)
    {
        var items = new List<BaselineComparisonItem>();

        // 実測側の辞書を構築 (同一モジュール名は最後のエントリ)
        var actualMap = new Dictionary<string, ExportHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in actualHistory)
        {
            actualMap[entry.ModuleName] = entry;
        }

        // 1. ベースライン側の各モジュールを基準に比較
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

        // 2. 実測側にのみ存在するモジュール (ベースラインにない)
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

        return new BaselineComparisonReport
        {
            PcName = pcName,
            Items = items,
        };
    }
}
