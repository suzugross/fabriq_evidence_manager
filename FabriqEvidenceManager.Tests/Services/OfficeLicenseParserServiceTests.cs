using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class OfficeLicenseParserServiceTests
{
    private readonly OfficeLicenseParserService _sut = new();

    [Fact]
    public void Parse_OobGraceWithC2RAndOspp_ExtractsAllBlocks()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Licenses"), "office_oob_grace.txt");

        var result = _sut.Parse(path);

        // インストール状態
        Assert.True(result.IsOfficeInstalled);

        // C2R
        Assert.NotNull(result.ClickToRun);
        Assert.Equal("ProPlus2024Volume", result.ClickToRun!.ProductReleaseIds);
        Assert.Equal("16.0.17932.20742", result.ClickToRun.VersionToReport);
        Assert.Equal("x64", result.ClickToRun.Platform);
        Assert.Equal("ja-jp", result.ClickToRun.ClientCulture);

        // OSPP path
        Assert.NotNull(result.OsppPath);
        Assert.Contains("OSPP.vbs", result.OsppPath);

        // OSPP /dstatus 製品ブロックが OOB_GRACE で抽出されること
        Assert.Single(result.Products);
        var product = result.Products[0];
        Assert.Contains("Office24ProPlus2024VL_KMS_Client_AE", product.LicenseName);
        Assert.Equal("OOB_GRACE", product.LicenseStatusText);
        Assert.False(product.IsLicensed);
        Assert.Equal("0x4004F00C", product.ErrorCode);
        Assert.Equal("GCVGB", product.LastFiveCharsProductKey);
    }
}
