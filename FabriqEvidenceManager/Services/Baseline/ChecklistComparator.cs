using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// チェックリスト HTML の OverallStatus + verify-table 各 ItemName×Status を照合する。
/// 結果は <see cref="BaselineComparisonReport.ChecklistComparison"/> に書き込む。
/// </summary>
public sealed class ChecklistComparator : IBaselineComparator
{
    private readonly IChecklistParserService _checklistParser;
    private ChecklistResult? _baseline;

    public string CategoryId => "Checklist";
    public string DisplayName => "チェックリスト";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baseline is not null;

    public ChecklistComparator(IChecklistParserService checklistParser)
    {
        _checklistParser = checklistParser;
    }

    public void CacheBaseline(PcEvidence baselinePc)
    {
        _baseline = TryParseChecklist(baselinePc.ChecklistFilePath);
    }

    public void ClearBaseline() => _baseline = null;

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (_baseline is null) return;
        var actual = TryParseChecklist(targetPc.ChecklistFilePath);
        if (actual is null) return;

        // VerifyItems を ItemName をキーに辞書化 (同名は最後勝ち)
        var actualVerifyMap = actual.VerifyItems
            .GroupBy(v => v.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var verifyComparisons = new List<VerificationItem>();
        foreach (var baselineItem in _baseline.VerifyItems)
        {
            if (actualVerifyMap.TryGetValue(baselineItem.ItemName, out var actualItem))
            {
                verifyComparisons.Add(new VerificationItem
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
                verifyComparisons.Add(new VerificationItem
                {
                    ItemName = baselineItem.ItemName,
                    Expected = baselineItem.Status,
                    Actual = "(未検出)",
                    Status = VerificationStatus.NoActual,
                });
            }
        }

        report.ChecklistComparison = new ChecklistComparisonResult
        {
            BaselineOverallStatus = _baseline.OverallStatus,
            ActualOverallStatus = actual.OverallStatus,
            VerifyItemComparisons = verifyComparisons,
        };
    }

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
