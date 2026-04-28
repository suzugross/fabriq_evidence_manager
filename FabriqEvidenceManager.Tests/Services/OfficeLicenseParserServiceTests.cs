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

    [Fact]
    public void Parse_LegacyEvidence_v15FieldsAreNull()
    {
        // 旧 evidence (v1.4 以前) は INTERPRETATION ブロックも DetectedAsSubscription 行も無いので
        // M11 で追加された属性は null (= 後方互換) になる。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("Licenses"), "office_oob_grace.txt");

        var result = _sut.Parse(path);

        Assert.Null(result.IsSubscriptionDetected);
        Assert.Null(result.OsppShowsGrace);
        Assert.Null(result.InterpretationConclusion);
        Assert.Null(result.ProvisionedCount);
        Assert.Empty(result.VnextEntries);
    }

    [Fact]
    public void Parse_V15SubscriptionProvisioned_ExtractsInterpretationAndVnext()
    {
        var dir = TestPaths.GetTestDataDirectory("Licenses");
        var txt = Path.Combine(dir, "office_v15_subscription_provisioned.txt");
        var csv = Path.Combine(dir, "office_v15_subscription_provisioned.csv");

        var result = _sut.Parse(txt, csv);

        // 22a: DetectedAsSubscription 行が拾えていること
        Assert.True(result.IsSubscriptionDetected);
        Assert.Equal("O365BusinessRetail", result.ClickToRun?.ProductReleaseIds);

        // 22d INTERPRETATION
        Assert.True(result.OsppShowsGrace);
        Assert.Equal(2, result.ProvisionedCount);
        Assert.NotNull(result.InterpretationConclusion);
        Assert.Contains("LICENSED", result.InterpretationConclusion);
        Assert.Contains("M365 subscription", result.InterpretationConclusion);

        // 22c CSV (2 件 / 両方 Provisioned)
        Assert.Equal(2, result.VnextEntries.Count);
        Assert.All(result.VnextEntries, e => Assert.True(e.IsProvisioned));
        Assert.Equal("test", result.VnextEntries[0].UserProfile);
        Assert.Equal("y_suzuki", result.VnextEntries[1].UserProfile);
        Assert.Equal("eda0d60f-4296-4900-87f7-3302d1e3db03", result.VnextEntries[0].TenantId);
        Assert.Equal("(present)", result.VnextEntries[0].HardwareIdBound);
        Assert.Equal("OK", result.VnextEntries[0].ParseStatus);

        // 既存 OSPP Grace は引き続き取れる (subscription 上は EXPECTED)
        Assert.Single(result.Products);
        Assert.Equal("NOTIFICATIONS", result.Products[0].LicenseStatusText);
    }

    [Fact]
    public void Parse_V15SubscriptionPartial_NoProvisionedAndConclusionIndicatesPending()
    {
        var dir = TestPaths.GetTestDataDirectory("Licenses");
        var txt = Path.Combine(dir, "office_v15_subscription_partial.txt");
        var csv = Path.Combine(dir, "office_v15_subscription_partial.csv");

        var result = _sut.Parse(txt, csv);

        Assert.True(result.IsSubscriptionDetected);
        Assert.Equal(0, result.ProvisionedCount);
        Assert.Empty(result.VnextEntries);
        Assert.NotNull(result.InterpretationConclusion);
        Assert.Contains("M365 subscription installed but no Provisioned", result.InterpretationConclusion);
    }

    [Fact]
    public void Parse_V15WithoutCsvPath_VnextEntriesEmptyButInterpretationStillRead()
    {
        // CSV ファイル不在 (旧 evidence や CSV 取得失敗時) でも txt 側の INTERPRETATION は読める。
        var dir = TestPaths.GetTestDataDirectory("Licenses");
        var txt = Path.Combine(dir, "office_v15_subscription_provisioned.txt");

        var result = _sut.Parse(txt, vnextCsvPath: null);

        Assert.Empty(result.VnextEntries);
        Assert.True(result.IsSubscriptionDetected);
        Assert.Equal(2, result.ProvisionedCount);
        Assert.NotNull(result.InterpretationConclusion);
    }
}
