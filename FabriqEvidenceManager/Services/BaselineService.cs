using System.IO;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// パイロット実行のベースライン管理。
/// export_history CSV を読み込み、ModuleName をキーにした辞書を保持する。
/// LoadFromPc で SystemInfo / チェックリストもキャッシュし、CompareAll で全カテゴリ一括照合する。
/// </summary>
public sealed class BaselineService : IBaselineService
{
    private readonly IChecklistParserService _checklistParser;
    private readonly IEvidenceParserService _parser;

    /// <summary>ModuleName → ExportHistoryEntry (ベースライン側)</summary>
    private Dictionary<string, ExportHistoryEntry> _baselineMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>ベースラインの全エントリ (順序保持)</summary>
    private List<ExportHistoryEntry> _baselineEntries = [];

    /// <summary>ベースラインPCのシステム基本情報</summary>
    private SystemInfoData? _baselineSystemInfo;

    /// <summary>ベースラインPCのチェックリスト結果</summary>
    private ChecklistResult? _baselineChecklist;

    /// <inheritdoc />
    public string? BaselinePcName { get; private set; }

    public BaselineService(IChecklistParserService checklistParser, IEvidenceParserService parser)
    {
        _checklistParser = checklistParser;
        _parser = parser;
    }

    /// <inheritdoc />
    public bool IsLoaded => _baselineEntries.Count > 0 || _baselineSystemInfo is not null;

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
    public void LoadFromPc(PcEvidence baselinePc)
    {
        BaselinePcName = baselinePc.PcName;

        // 1. export_history CSV (実行サマリ)
        if (baselinePc.ExportHistoryFilePath is not null && File.Exists(baselinePc.ExportHistoryFilePath))
        {
            Load(baselinePc.ExportHistoryFilePath);
        }

        // 2. SystemInfo (01_SystemInfo.txt)
        _baselineSystemInfo = TryParseSystemInfo(baselinePc.PcInformationDirectoryPath);

        // 3. チェックリスト HTML
        _baselineChecklist = TryParseChecklist(baselinePc.ChecklistFilePath);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _baselineEntries.Clear();
        _baselineMap.Clear();
        _baselineSystemInfo = null;
        _baselineChecklist = null;
        BaselinePcName = null;
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

    /// <inheritdoc />
    public BaselineComparisonReport CompareAll(PcEvidence targetPc)
    {
        // 1. 実行サマリ照合 (既存ロジック)
        List<BaselineComparisonItem> items = [];
        if (_baselineEntries.Count > 0 && targetPc.ExportHistoryFilePath is not null
            && File.Exists(targetPc.ExportHistoryFilePath))
        {
            var actualHistory = _checklistParser.ParseExportHistory(targetPc.ExportHistoryFilePath);
            var summaryReport = Compare(targetPc.PcName, actualHistory);
            items = summaryReport.Items;
        }

        // 2. SystemInfo 照合
        var sysInfoComparison = CompareSystemInfo(targetPc);

        // 3. チェックリスト照合
        var checklistComparison = CompareChecklist(targetPc);

        return new BaselineComparisonReport
        {
            PcName = targetPc.PcName,
            Items = items,
            SystemInfoComparison = sysInfoComparison,
            ChecklistComparison = checklistComparison,
        };
    }

    // ================================================================
    // SystemInfo 照合
    // ================================================================

    private SystemInfoComparisonResult? CompareSystemInfo(PcEvidence targetPc)
    {
        if (_baselineSystemInfo is null)
            return null;

        var actualSystemInfo = TryParseSystemInfo(targetPc.PcInformationDirectoryPath);
        if (actualSystemInfo is null)
            return null;

        var items = new List<VerificationItem>
        {
            CompareField("OS名", _baselineSystemInfo.OsName, actualSystemInfo.OsName),
            CompareField("OSバージョン", _baselineSystemInfo.Version, actualSystemInfo.Version),
            CompareField("CPU", _baselineSystemInfo.Cpu, actualSystemInfo.Cpu),
            CompareField("メモリ", _baselineSystemInfo.Memory, actualSystemInfo.Memory),
        };

        return new SystemInfoComparisonResult { Items = items };
    }

    // ================================================================
    // チェックリスト照合
    // ================================================================

    private ChecklistComparisonResult? CompareChecklist(PcEvidence targetPc)
    {
        if (_baselineChecklist is null)
            return null;

        var actualChecklist = TryParseChecklist(targetPc.ChecklistFilePath);
        if (actualChecklist is null)
            return null;

        // VerifyItems の照合: ItemName をキーに Status を比較
        var verifyItemComparisons = new List<VerificationItem>();

        // ベースライン側の VerifyItems を基準に照合
        var actualVerifyMap = actualChecklist.VerifyItems
            .GroupBy(v => v.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var baselineItem in _baselineChecklist.VerifyItems)
        {
            if (actualVerifyMap.TryGetValue(baselineItem.ItemName, out var actualItem))
            {
                verifyItemComparisons.Add(new VerificationItem
                {
                    ItemName = baselineItem.ItemName,
                    Expected = baselineItem.Status,
                    Actual = actualItem.Status,
                    Status = baselineItem.Status.Equals(actualItem.Status, StringComparison.OrdinalIgnoreCase)
                        ? VerificationStatus.Match
                        : VerificationStatus.Mismatch,
                });
            }
            else
            {
                verifyItemComparisons.Add(new VerificationItem
                {
                    ItemName = baselineItem.ItemName,
                    Expected = baselineItem.Status,
                    Actual = "(未検出)",
                    Status = VerificationStatus.NoActual,
                });
            }
        }

        return new ChecklistComparisonResult
        {
            BaselineOverallStatus = _baselineChecklist.OverallStatus,
            ActualOverallStatus = actualChecklist.OverallStatus,
            VerifyItemComparisons = verifyItemComparisons,
        };
    }

    // ================================================================
    // ヘルパー
    // ================================================================

    /// <summary>文字列フィールドを比較して VerificationItem を生成する。</summary>
    private static VerificationItem CompareField(string itemName, string expected, string actual)
    {
        var expTrimmed = expected.Trim();
        var actTrimmed = actual.Trim();

        VerificationStatus status;
        if (string.IsNullOrEmpty(expTrimmed) && string.IsNullOrEmpty(actTrimmed))
            status = VerificationStatus.NoExpected;
        else if (string.IsNullOrEmpty(expTrimmed))
            status = VerificationStatus.NoExpected;
        else if (string.IsNullOrEmpty(actTrimmed))
            status = VerificationStatus.NoActual;
        else
            status = expTrimmed.Equals(actTrimmed, StringComparison.OrdinalIgnoreCase)
                ? VerificationStatus.Match
                : VerificationStatus.Mismatch;

        return new VerificationItem
        {
            ItemName = itemName,
            Expected = expTrimmed,
            Actual = actTrimmed,
            Status = status,
        };
    }

    /// <summary>pc_information ディレクトリから SystemInfo をパースする。失敗時は null。</summary>
    private SystemInfoData? TryParseSystemInfo(string? pcInfoDir)
    {
        if (pcInfoDir is null) return null;
        var path = Path.Combine(pcInfoDir, EvidenceConstants.FileSystemInfo);
        if (!File.Exists(path)) return null;

        try
        {
            return _parser.ParseSystemInfo(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Baseline Skip] SystemInfo: {ex.Message}");
            return null;
        }
    }

    /// <summary>チェックリスト HTML をパースする。失敗時は null。</summary>
    private ChecklistResult? TryParseChecklist(string? checklistPath)
    {
        if (checklistPath is null || !File.Exists(checklistPath)) return null;

        try
        {
            return _checklistParser.ParseChecklistHtml(checklistPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Baseline Skip] Checklist: {ex.Message}");
            return null;
        }
    }
}
