using System;
using System.Collections.Generic;
using System.IO;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// 11_StoreApps.csv のみを対象としたベースライン突合 (§11 part 2)。
/// アプリ名 (Name, case-insensitive) をキーに Version を比較し、差分のみ Items に格納。
/// 結果は <see cref="BaselineComparisonReport.StoreAppsComparison"/> に書き込む。
/// </summary>
public sealed class StoreAppsComparator : IBaselineComparator
{
    private readonly IEvidenceParserService _parser;
    private List<InstalledAppEntry>? _baseline;

    public string CategoryId => "StoreApps";
    public string DisplayName => "ストアアプリ";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baseline is not null;

    public StoreAppsComparator(IEvidenceParserService parser)
    {
        _parser = parser;
    }

    public void CacheBaseline(PcEvidence baselinePc)
    {
        _baseline = TryParseStoreApps(baselinePc.PcInformationDirectoryPath);
    }

    public void ClearBaseline() => _baseline = null;

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (_baseline is null) return;
        var actualApps = TryParseStoreApps(targetPc.PcInformationDirectoryPath);
        if (actualApps is null) return;

        report.StoreAppsComparison = InstalledAppsCompareLogic.Compare(_baseline, actualApps);
    }

    private List<InstalledAppEntry>? TryParseStoreApps(string? pcInfoDir)
    {
        if (pcInfoDir is null) return null;

        var path = Path.Combine(pcInfoDir, EvidenceConstants.FileStoreApps);
        if (!File.Exists(path)) return null;

        try
        {
            var apps = _parser.ParseStoreApps(path);
            return apps.Count > 0 ? new List<InstalledAppEntry>(apps) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Baseline Skip] StoreApps: {ex.Message}");
            return null;
        }
    }
}
