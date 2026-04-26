using System;
using System.Collections.Generic;
using System.IO;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// 11_DesktopApps.csv + 11_StoreApps.csv からインストール済みアプリを統合し、
/// アプリ名×バージョンで照合する。差分のみ Items に格納し、一致は MatchCount で集計。
/// 結果は <see cref="BaselineComparisonReport.InstalledAppsComparison"/> に書き込む。
/// </summary>
public sealed class InstalledAppsComparator : IBaselineComparator
{
    private readonly IEvidenceParserService _parser;
    private List<InstalledAppEntry>? _baseline;

    public string CategoryId => "InstalledApps";
    public string DisplayName => "インストール済みアプリ";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baseline is not null;

    public InstalledAppsComparator(IEvidenceParserService parser)
    {
        _parser = parser;
    }

    public void CacheBaseline(PcEvidence baselinePc)
    {
        _baseline = TryParseInstalledApps(baselinePc.PcInformationDirectoryPath);
    }

    public void ClearBaseline() => _baseline = null;

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (_baseline is null) return;
        var actualApps = TryParseInstalledApps(targetPc.PcInformationDirectoryPath);
        if (actualApps is null) return;

        // Name (case-insensitive) をキーに辞書化 (同名は最後勝ち)
        var baselineMap = new Dictionary<string, InstalledAppEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in _baseline)
            baselineMap[app.Name] = app;

        var actualMap = new Dictionary<string, InstalledAppEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in actualApps)
            actualMap[app.Name] = app;

        var diffItems = new List<VerificationItem>();
        var matchCount = 0;

        // ベースライン側の各アプリを基準に比較
        foreach (var (name, baseline) in baselineMap)
        {
            if (actualMap.TryGetValue(name, out var actual))
            {
                if (baseline.Version.Equals(actual.Version, StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                }
                else
                {
                    diffItems.Add(new VerificationItem
                    {
                        ItemName = name,
                        Expected = baseline.Version,
                        Actual = actual.Version,
                        Status = VerificationStatus.Mismatch,
                    });
                }
            }
            else
            {
                diffItems.Add(new VerificationItem
                {
                    ItemName = name,
                    Expected = baseline.Version,
                    Actual = "(未検出)",
                    Status = VerificationStatus.NoActual,
                });
            }
        }

        // 対象 PC のみ存在するアプリ
        foreach (var (name, actual) in actualMap)
        {
            if (!baselineMap.ContainsKey(name))
            {
                diffItems.Add(new VerificationItem
                {
                    ItemName = name,
                    Expected = "(ベースラインなし)",
                    Actual = actual.Version,
                    Status = VerificationStatus.NoExpected,
                });
            }
        }

        report.InstalledAppsComparison = new InstalledAppsComparisonResult
        {
            Items = diffItems,
            TotalBaselineCount = baselineMap.Count,
            TotalActualCount = actualMap.Count,
            MatchCount = matchCount,
        };
    }

    private List<InstalledAppEntry>? TryParseInstalledApps(string? pcInfoDir)
    {
        if (pcInfoDir is null) return null;

        var desktopPath = Path.Combine(pcInfoDir, EvidenceConstants.FileDesktopApps);
        var storePath = Path.Combine(pcInfoDir, EvidenceConstants.FileStoreApps);

        if (!File.Exists(desktopPath) && !File.Exists(storePath))
            return null;

        var combined = new List<InstalledAppEntry>();

        try
        {
            if (File.Exists(desktopPath))
                combined.AddRange(_parser.ParseDesktopApps(desktopPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Baseline Skip] DesktopApps: {ex.Message}");
        }

        try
        {
            if (File.Exists(storePath))
                combined.AddRange(_parser.ParseStoreApps(storePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"[Baseline Skip] StoreApps: {ex.Message}");
        }

        return combined.Count > 0 ? combined : null;
    }
}
