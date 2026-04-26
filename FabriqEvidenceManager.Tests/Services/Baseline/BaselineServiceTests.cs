using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Services.Baseline;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services.Baseline;

/// <summary>
/// BaselineService が登録済み IBaselineComparator 実装をオーケストレートする
/// 振る舞いの統合テスト (Phase 1 リファクタの根幹を守る)。
/// </summary>
public class BaselineServiceTests
{
    [Fact]
    public void IsLoaded_WithoutAnyData_ReturnsFalse()
    {
        var sut = MakeService();
        Assert.False(sut.IsLoaded);
        Assert.Null(sut.BaselinePcName);
    }

    [Fact]
    public void LoadFromPc_WithLicenseData_BecomesLoaded()
    {
        var sut = MakeService();
        var pc = new PcEvidence { PcName = "BASE", CollectionDate = "2026" };
        pc.WindowsLicense = new WindowsLicenseData { Products = [], Service = null };

        sut.LoadFromPc(pc);

        Assert.True(sut.IsLoaded);
        Assert.Equal("BASE", sut.BaselinePcName);
    }

    [Fact]
    public void Clear_AfterLoad_EmptiesAllState()
    {
        var sut = MakeService();
        var pc = new PcEvidence { PcName = "BASE", CollectionDate = "2026" };
        pc.WindowsLicense = new WindowsLicenseData { Products = [], Service = null };
        pc.OfficeLicense = new OfficeLicenseData { IsOfficeInstalled = false, Products = [] };
        sut.LoadFromPc(pc);
        Assert.True(sut.IsLoaded);

        sut.Clear();

        Assert.False(sut.IsLoaded);
        Assert.Null(sut.BaselinePcName);
    }

    [Fact]
    public void CompareAll_WithLicenseOnly_PopulatesOnlyLicenseField()
    {
        // BaselineService は登録済み Comparator 全てを呼ぶが、
        // データがないカテゴリの Comparator は report に書き込まない
        var sut = MakeService();
        var baseline = new PcEvidence { PcName = "BASE", CollectionDate = "2026" };
        baseline.WindowsLicense = new WindowsLicenseData { Products = [], Service = null };
        baseline.OfficeLicense = new OfficeLicenseData { IsOfficeInstalled = false, Products = [] };
        sut.LoadFromPc(baseline);

        var target = new PcEvidence { PcName = "TARGET", CollectionDate = "2026" };
        target.WindowsLicense = new WindowsLicenseData { Products = [], Service = null };
        target.OfficeLicense = new OfficeLicenseData { IsOfficeInstalled = false, Products = [] };

        var report = sut.CompareAll(target);

        // License は populated
        Assert.NotNull(report.LicenseComparison);
        // 他カテゴリ (SystemInfo / Checklist / InstalledApps / ExecutionSummary) はデータ無し
        Assert.Null(report.SystemInfoComparison);
        Assert.Null(report.ChecklistComparison);
        Assert.Null(report.InstalledAppsComparison);
        Assert.Empty(report.Items);
    }

    /// <summary>5 つの Comparator (License/SystemInfo/Checklist/InstalledApps/ExecutionSummary) を全て登録</summary>
    private static BaselineService MakeService()
    {
        var winParser = new WindowsLicenseParserService();
        var officeParser = new OfficeLicenseParserService();
        var parser = new EvidenceParserService(winParser, officeParser);
        var checklistParser = new ChecklistParserService();

        var comparators = new IBaselineComparator[]
        {
            new ExecutionSummaryComparator(checklistParser),
            new SystemInfoComparator(parser),
            new ChecklistComparator(checklistParser),
            new InstalledAppsComparator(parser),
            new LicenseComparator(),
        };

        return new BaselineService(comparators);
    }
}
