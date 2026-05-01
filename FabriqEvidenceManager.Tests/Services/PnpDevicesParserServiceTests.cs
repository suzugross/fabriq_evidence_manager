using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class PnpDevicesParserServiceTests
{
    private readonly PnpDevicesParserService _sut = new();

    [Fact]
    public void Parse_FullModernPc_All343EntriesParsed()
    {
        // 実フィクスチャ (ASUS EXPERTBOOK B1402CVA / 343 デバイス)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("PnpDevices"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);

        Assert.Equal(343, entries.Count);
    }

    [Fact]
    public void Parse_FullModernPc_PresentVsCachedCounts()
    {
        // Phase C "Present: M / Disconnected: N" 表示の前提を保証
        var path = Path.Combine(TestPaths.GetTestDataDirectory("PnpDevices"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);

        Assert.Equal(188, entries.Count(e => e.Present));
        Assert.Equal(155, entries.Count(e => !e.Present));
    }

    [Fact]
    public void Parse_FullModernPc_RealtekSpeakerEntryFullyParsed()
    {
        // 特定エントリ (スピーカー Realtek Audio) の全フィールドを検証
        var path = Path.Combine(TestPaths.GetTestDataDirectory("PnpDevices"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var speaker = entries.Single(e => e.FriendlyName == "スピーカー (3- Realtek(R) Audio)");

        Assert.Equal("AudioEndpoint", speaker.Class);
        Assert.Equal("OK", speaker.Status);
        Assert.True(speaker.Present);
        Assert.Equal("Microsoft", speaker.Manufacturer);
        Assert.Equal("10.0.26100.1", speaker.DriverVersion);
        Assert.Equal("2024-03-31", speaker.DriverDate);
        Assert.Contains("MMDEVAPI", speaker.InstanceId);
    }

    [Fact]
    public void Parse_FullModernPc_LowLevelDeviceWithEmptyClassParsed()
    {
        // Class 列が空のエントリ (HTREE\ROOT\0 のような低レベルバスデバイス) も
        // 空文字列として保持され、行落ちしないことを保証する。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("PnpDevices"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var rootDevice = entries.Single(e => e.InstanceId == "HTREE\\ROOT\\0");

        Assert.Equal(string.Empty, rootDevice.Class);
        Assert.Equal(string.Empty, rootDevice.FriendlyName);
        Assert.Equal("OK", rootDevice.Status);
        Assert.True(rootDevice.Present);
    }

    [Fact]
    public void Parse_FullModernPc_StatusValuesAreOkOrUnknown()
    {
        // Status 列の代表値 ("OK" / "Unknown") が CSV から正しく抽出されることを確認
        var path = Path.Combine(TestPaths.GetTestDataDirectory("PnpDevices"), "full_modern_pc.csv");

        var entries = _sut.Parse(path);
        var statuses = entries.Select(e => e.Status).Distinct().ToList();

        Assert.Contains("OK", statuses);
        Assert.Contains("Unknown", statuses);
    }

    [Fact]
    public void Parse_EmptyCsv_ReturnsEmptyList()
    {
        // ヘッダのみで行なし → 空コレクション (例外なし)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("PnpDevices"), "empty.csv");

        var entries = _sut.Parse(path);

        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_MissingFile_ReturnsEmptyList()
    {
        var entries = _sut.Parse(Path.Combine(TestPaths.GetTestDataDirectory("PnpDevices"), "does_not_exist.csv"));

        Assert.Empty(entries);
    }
}
