using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

/// <summary>
/// EvidenceParserService.ParseDesktopApps / ParseStoreApps の検証。
/// §11 はソースが Desktop / Store の 2 系統で CSV スキーマも異なる
/// (Desktop は InstallDate 列あり、Store はなし)。
/// Phase 1 で PcEvidence.DesktopApps / StoreApps への自動取り込みが追加されたため
/// 既存パーサ実装が新フィクスチャ (v1.6.0 evidence) でも正しく動くことを保証する。
/// </summary>
public class InstalledAppsParserTests
{
    private readonly EvidenceParserService _sut = MakeParser();

    [Fact]
    public void ParseDesktopApps_FullModernPc_Has41Entries()
    {
        // 実フィクスチャ (test01_3WNCV04 / v1.6.0+ / 41 件)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("InstalledApps"), "full_modern_pc_desktop.csv");

        var apps = _sut.ParseDesktopApps(path);

        Assert.Equal(41, apps.Count);
        Assert.All(apps, a => Assert.Equal(InstalledAppSource.Desktop, a.Source));
    }

    [Fact]
    public void ParseDesktopApps_FullModernPc_AdobeAcrobatEntryFullyParsed()
    {
        // 特定エントリ (Adobe Acrobat 64-bit) の全 4 フィールドを検証
        var path = Path.Combine(TestPaths.GetTestDataDirectory("InstalledApps"), "full_modern_pc_desktop.csv");

        var apps = _sut.ParseDesktopApps(path);
        var adobe = apps.Single(a => a.Name == "Adobe Acrobat (64-bit)");

        Assert.Equal("25.001.21288", adobe.Version);
        Assert.Equal("Adobe", adobe.Publisher);
        Assert.Equal("20260507", adobe.InstallDate);
        Assert.Equal(InstalledAppSource.Desktop, adobe.Source);
    }

    [Fact]
    public void ParseStoreApps_FullModernPc_Has104Entries()
    {
        // 実フィクスチャ (104 件)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("InstalledApps"), "full_modern_pc_store.csv");

        var apps = _sut.ParseStoreApps(path);

        Assert.Equal(104, apps.Count);
        Assert.All(apps, a => Assert.Equal(InstalledAppSource.Store, a.Source));
    }

    [Fact]
    public void ParseStoreApps_FullModernPc_InstallDateIsEmpty()
    {
        // §11 StoreApps は CSV に InstallDate 列を持たない仕様。
        // 全エントリで InstallDate が空文字列であることを保証。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("InstalledApps"), "full_modern_pc_store.csv");

        var apps = _sut.ParseStoreApps(path);

        Assert.All(apps, a => Assert.Equal(string.Empty, a.InstallDate));
    }

    [Fact]
    public void ParseStoreApps_FullModernPc_AdobeReaderCoreEntryFullyParsed()
    {
        // 特定エントリ (AdobeAcrobatReaderCoreApp) の 3 フィールドを検証
        var path = Path.Combine(TestPaths.GetTestDataDirectory("InstalledApps"), "full_modern_pc_store.csv");

        var apps = _sut.ParseStoreApps(path);
        var adobe = apps.Single(a => a.Name == "AdobeAcrobatReaderCoreApp");

        Assert.Equal("24.0.0.1", adobe.Version);
        Assert.Equal("pc75e8sa7ep4e", adobe.Publisher);
        Assert.Equal(InstalledAppSource.Store, adobe.Source);
    }

    [Fact]
    public void ParseDesktopApps_EmptyCsv_ReturnsEmptyList()
    {
        var path = Path.Combine(TestPaths.GetTestDataDirectory("InstalledApps"), "empty_desktop.csv");

        var apps = _sut.ParseDesktopApps(path);

        Assert.Empty(apps);
    }

    /// <summary>11 引数のコンストラクタを抜けて EvidenceParserService を組み立てるテスト用ファクトリ。</summary>
    private static EvidenceParserService MakeParser()
        => new(new WindowsLicenseParserService(), new OfficeLicenseParserService(),
               new SecurityBaselineParserService(), new CertificatesParserService(),
               new BatteryReportParserService(), new HardwareIdentifiersParserService(),
               new MemoryInventoryParserService(), new EnvironmentVariablesParserService(),
               new StartupItemsParserService(), new PnpDevicesParserService(),
               new GroupPolicyParserService());
}
