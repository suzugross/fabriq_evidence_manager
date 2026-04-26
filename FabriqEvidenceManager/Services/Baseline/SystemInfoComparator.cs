using System;
using System.Collections.Generic;
using System.IO;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// 01_SystemInfo.txt から OS名 / バージョン / CPU / メモリ の固定 4 項目を照合する。
/// 結果は <see cref="BaselineComparisonReport.SystemInfoComparison"/> に書き込む。
/// </summary>
public sealed class SystemInfoComparator : IBaselineComparator
{
    private readonly IEvidenceParserService _parser;
    private SystemInfoData? _baseline;

    public string CategoryId => "SystemInfo";
    public string DisplayName => "SystemInfo";
    public bool IsEnabledByDefault => true;
    public bool HasBaselineData => _baseline is not null;

    public SystemInfoComparator(IEvidenceParserService parser)
    {
        _parser = parser;
    }

    public void CacheBaseline(PcEvidence baselinePc)
    {
        // 既存挙動: 取得失敗時は null セット (前回キャッシュを破棄)
        _baseline = TryParseSystemInfo(baselinePc.PcInformationDirectoryPath);
    }

    public void ClearBaseline() => _baseline = null;

    public void Compare(BaselineComparisonReport report, PcEvidence targetPc)
    {
        if (_baseline is null) return;
        var actual = TryParseSystemInfo(targetPc.PcInformationDirectoryPath);
        if (actual is null) return;

        var items = new List<VerificationItem>
        {
            CompareField("OS名", _baseline.OsName, actual.OsName),
            CompareField("OSバージョン", _baseline.Version, actual.Version),
            CompareField("CPU", _baseline.Cpu, actual.Cpu),
            CompareField("メモリ", _baseline.Memory, actual.Memory),
        };

        report.SystemInfoComparison = new SystemInfoComparisonResult { Items = items };
    }

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
}
