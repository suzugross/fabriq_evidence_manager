using System;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class CertificatesParserServiceTests
{
    private readonly CertificatesParserService _sut = new();

    [Fact]
    public void Parse_RealFixture9Rows_ExtractsAllColumnsCorrectly()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Certificates"), "sample_9rows.csv");

        var entries = _sut.Parse(path);

        Assert.Equal(9, entries.Count);

        var digicert = entries[0];
        Assert.Equal("LocalMachine\\Root", digicert.Store);
        Assert.Contains("DigiCert Trusted Root G4", digicert.Subject);
        Assert.Equal("DDFB16CD4931C973A2037D3FC83A4D7D775D05E4", digicert.Thumbprint);
        Assert.Equal(new DateTime(2013, 8, 1, 21, 0, 0), digicert.NotBefore);
        Assert.Equal(new DateTime(2038, 1, 15, 21, 0, 0), digicert.NotAfter);
        Assert.False(digicert.HasPrivateKey);
        Assert.Contains("クライアント認証", digicert.EnhancedKeyUsageList);
        Assert.Equal("059B1B579E8E2132E23907BDA777755C", digicert.SerialNumber);
    }

    [Fact]
    public void Parse_QuotedFieldsContainingComma_HandledByCsvParser()
    {
        // 行 3: Issuer="CN=DigiCert..., O=""DigiCert, Inc."", C=US" にカンマ + エスケープ "" を含む
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Certificates"), "sample_9rows.csv");

        var entries = _sut.Parse(path);

        var logitech = entries[1];
        Assert.Contains("Logitech Inc", logitech.Subject);
        Assert.Contains("DigiCert, Inc.", logitech.Issuer);  // "" → " 復元 + カンマ含み
    }

    [Fact]
    public void DaysToExpiry_ReturnsCorrectDayCount_ForFixedReferenceDate()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Certificates"), "sample_9rows.csv");
        var reference = new DateTime(2026, 4, 28);

        var entries = _sut.Parse(path);

        // DigiCert: 2038/01/15 - 2026/04/28 = 4280 日程度
        Assert.NotNull(entries[0].DaysToExpiry(reference));
        Assert.True(entries[0].DaysToExpiry(reference) > 4000);

        // Microsoft Root (期限切れ): 2021/05/10 - 2026/04/28 = -1814 日程度
        Assert.NotNull(entries[2].DaysToExpiry(reference));
        Assert.True(entries[2].DaysToExpiry(reference) < 0);
    }

    [Fact]
    public void Parse_NonExistentFile_ReturnsEmptyList()
    {
        var entries = _sut.Parse(Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.csv"));
        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_HeaderOnlyFile_ReturnsEmptyList()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "\"Store\",\"Subject\",\"Issuer\",\"Thumbprint\",\"NotBefore\",\"NotAfter\",\"HasPrivateKey\",\"EnhancedKeyUsageList\",\"FriendlyName\",\"SerialNumber\"\n");
            var entries = _sut.Parse(tmp);
            Assert.Empty(entries);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
