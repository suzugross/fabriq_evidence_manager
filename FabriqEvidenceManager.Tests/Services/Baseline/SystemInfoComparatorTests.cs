using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Services.Baseline;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services.Baseline;

public class SystemInfoComparatorTests
{
    /// <summary>v3.3.1 回帰防止: 4 項目すべて空の SystemInfo 同士の比較で全 Match を返すこと。</summary>
    [Fact]
    public void Compare_BothPcsHaveAllEmptyFields_AllMatch()
    {
        var parser = MakeParser();
        var sut = new SystemInfoComparator(parser);
        var dir = TestPaths.GetTestDataDirectory("SystemInfo", "all_empty");

        var baseline = MakePc("BASE", dir);
        var target = MakePc("TARGET", dir);

        sut.CacheBaseline(baseline);
        var report = new BaselineComparisonReport { PcName = "TARGET" };
        sut.Compare(report, target);

        Assert.NotNull(report.SystemInfoComparison);
        Assert.True(report.SystemInfoComparison!.AllMatch);
        Assert.Equal(0, report.SystemInfoComparison.MismatchCount);
    }

    private static EvidenceParserService MakeParser()
        => new(new WindowsLicenseParserService(), new OfficeLicenseParserService(),
               new SecurityBaselineParserService(), new CertificatesParserService(),
               new BatteryReportParserService());

    private static PcEvidence MakePc(string name, string pcInfoDir)
    {
        var pc = new PcEvidence { PcName = name, CollectionDate = "2026_01_01" };
        pc.PcInformationDirectoryPath = pcInfoDir;
        return pc;
    }
}
