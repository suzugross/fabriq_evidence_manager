using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Baseline;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services.Baseline;

/// <summary>
/// LicenseComparator の境界条件テスト。
/// 特に v3.3.1 の「両方空 → Match」修正の回帰防止が主目的。
/// </summary>
public class LicenseComparatorTests
{
    [Fact]
    public void Compare_BothPcsHaveOfficeNotInstalled_AllItemsMatch()
    {
        // v3.3.1 回帰防止: ベースライン PC ⇄ 自身を比較したときに、
        // Office 未インストール (ClickToRun=null, Products=[]) で空フィールドが
        // false-positive (NEW = NoExpected) として検出されないこと。
        var sut = new LicenseComparator();

        var emptyOffice = new OfficeLicenseData
        {
            IsOfficeInstalled = false,
            ClickToRun = null,
            OsppPath = null,
            Products = [],
            OsppDstatusRaw = string.Empty,
        };
        var emptyWindows = new WindowsLicenseData
        {
            Products = [],
            Service = null,
            SlmgrDlvRaw = string.Empty,
        };

        var baseline = MakePc("BASE", emptyWindows, emptyOffice);
        var target = MakePc("TARGET", emptyWindows, emptyOffice);

        sut.CacheBaseline(baseline);
        var report = new BaselineComparisonReport { PcName = "TARGET" };
        sut.Compare(report, target);

        Assert.NotNull(report.LicenseComparison);
        Assert.True(report.LicenseComparison!.AllMatch);
        Assert.Equal(0, report.LicenseComparison.MismatchCount);
    }

    [Fact]
    public void Compare_DifferentLicenseFamily_DetectsMismatch()
    {
        var sut = new LicenseComparator();

        var winProfessional = new WindowsLicenseData
        {
            Products = new List<WindowsLicensingProduct>
            {
                new() { LicenseFamily = "Professional", LicenseStatusText = "Licensed", LicenseStatusCode = 1 },
            },
        };
        var winEnterprise = new WindowsLicenseData
        {
            Products = new List<WindowsLicensingProduct>
            {
                new() { LicenseFamily = "Enterprise", LicenseStatusText = "Licensed", LicenseStatusCode = 1 },
            },
        };
        var emptyOffice = new OfficeLicenseData
        {
            IsOfficeInstalled = false,
            Products = [],
        };

        var baseline = MakePc("BASE", winProfessional, emptyOffice);
        var target = MakePc("TARGET", winEnterprise, emptyOffice);

        sut.CacheBaseline(baseline);
        var report = new BaselineComparisonReport { PcName = "TARGET" };
        sut.Compare(report, target);

        Assert.NotNull(report.LicenseComparison);
        var familyItem = report.LicenseComparison!.Items.First(i => i.ItemName.Contains("ライセンスファミリ"));
        Assert.Equal(VerificationStatus.Mismatch, familyItem.Status);
        Assert.Equal("Professional", familyItem.Expected);
        Assert.Equal("Enterprise", familyItem.Actual);
    }

    private static PcEvidence MakePc(string name, WindowsLicenseData win, OfficeLicenseData office)
    {
        var pc = new PcEvidence { PcName = name, CollectionDate = "2026_01_01" };
        pc.WindowsLicense = win;
        pc.OfficeLicense = office;
        return pc;
    }
}
