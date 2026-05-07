using System;
using System.Collections.Generic;
using System.IO;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// 11_DesktopApps.csv のみを対象としたベースライン突合 (§11 part 1)。
/// アプリ名 (Name, case-insensitive) をキーに Version を比較し、差分のみ Items に格納。
/// 結果は <see cref="BaselineComparisonReport.DesktopAppsComparison"/> に書き込む。
/// </summary>
public sealed class DesktopAppsComparator : IBaselineComparator
{
    private readonly IEvidenceParserService _parser;
    private List<InstalledAppEntry>? _baseline;

    public string CategoryId => "DesktopApps";
    public string DisplayName => "デスクトップアプリ";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baseline is not null;

    public DesktopAppsComparator(IEvidenceParserService parser)
    {
        _parser = parser;
    }

    public void CacheBaseline(PcEvidence baselinePc)
    {
        _baseline = TryParseDesktopApps(baselinePc.PcInformationDirectoryPath);
    }

    public void ClearBaseline() => _baseline = null;

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (_baseline is null) return;
        var actualApps = TryParseDesktopApps(targetPc.PcInformationDirectoryPath);
        if (actualApps is null) return;

        report.DesktopAppsComparison = InstalledAppsCompareLogic.Compare(_baseline, actualApps);
    }

    private List<InstalledAppEntry>? TryParseDesktopApps(string? pcInfoDir)
    {
        if (pcInfoDir is null) return null;

        var path = Path.Combine(pcInfoDir, EvidenceConstants.FileDesktopApps);
        if (!File.Exists(path)) return null;

        try
        {
            var apps = _parser.ParseDesktopApps(path);
            return apps.Count > 0 ? new List<InstalledAppEntry>(apps) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Baseline Skip] DesktopApps: {ex.Message}");
            return null;
        }
    }
}
