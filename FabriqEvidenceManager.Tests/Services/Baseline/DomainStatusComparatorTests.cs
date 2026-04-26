using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Baseline;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services.Baseline;

public class DomainStatusComparatorTests
{
    [Fact]
    public void Compare_IdenticalDomainStatus_AllMatch()
    {
        var sut = new DomainStatusComparator();
        var domainStatus = new DomainStatusData
        {
            PartOfDomain = true,
            Domain = "corp.example.com",
            DomainRole = "MemberWorkstation",
            AzureAdJoined = false,
            DomainJoined = true,
            DomainName = "corp.example.com",
            TenantName = string.Empty,
        };

        var baseline = MakePc("BASE", domainStatus);
        var target = MakePc("TARGET", domainStatus);

        sut.CacheBaseline(baseline);
        var report = new BaselineComparisonReport { PcName = "TARGET" };
        sut.Compare(report, target);

        Assert.NotNull(report.DomainStatusComparison);
        Assert.True(report.DomainStatusComparison!.AllMatch);
        Assert.Equal(0, report.DomainStatusComparison.MismatchCount);
        Assert.Equal(7, report.DomainStatusComparison.Items.Count);
    }

    [Fact]
    public void Compare_DifferentDomain_DetectsMismatch()
    {
        var sut = new DomainStatusComparator();
        var baselineDomain = new DomainStatusData
        {
            PartOfDomain = true,
            Domain = "corp.example.com",
            DomainJoined = true,
            DomainName = "corp.example.com",
        };
        var targetDomain = new DomainStatusData
        {
            PartOfDomain = true,
            Domain = "alt.example.com",     // 違うドメインに参加
            DomainJoined = true,
            DomainName = "alt.example.com",
        };

        var baseline = MakePc("BASE", baselineDomain);
        var target = MakePc("TARGET", targetDomain);

        sut.CacheBaseline(baseline);
        var report = new BaselineComparisonReport { PcName = "TARGET" };
        sut.Compare(report, target);

        Assert.NotNull(report.DomainStatusComparison);
        Assert.False(report.DomainStatusComparison!.AllMatch);
        var domainItem = report.DomainStatusComparison.Items.First(i => i.ItemName == "ドメイン名");
        Assert.Equal(VerificationStatus.Mismatch, domainItem.Status);
        Assert.Equal("corp.example.com", domainItem.Expected);
        Assert.Equal("alt.example.com", domainItem.Actual);
    }

    [Fact]
    public void Compare_BaselineHasDomainTargetWorkgroup_DetectsMismatchOnPartOfDomain()
    {
        var sut = new DomainStatusComparator();
        var baseline = MakePc("BASE", new DomainStatusData { PartOfDomain = true, Domain = "corp.example.com" });
        var target = MakePc("TARGET", new DomainStatusData { PartOfDomain = false });

        sut.CacheBaseline(baseline);
        var report = new BaselineComparisonReport { PcName = "TARGET" };
        sut.Compare(report, target);

        var partOfDomainItem = report.DomainStatusComparison!.Items.First(i => i.ItemName == "ドメイン参加");
        Assert.Equal(VerificationStatus.Mismatch, partOfDomainItem.Status);
        Assert.Equal("参加", partOfDomainItem.Expected);
        Assert.Equal("未参加", partOfDomainItem.Actual);
    }

    private static PcEvidence MakePc(string name, DomainStatusData domain)
    {
        var pc = new PcEvidence { PcName = name, CollectionDate = "2026_01_01" };
        pc.DomainStatus = domain;
        return pc;
    }
}
