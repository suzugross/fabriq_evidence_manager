using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Tests.Helpers;
using Xunit;

namespace FabriqEvidenceManager.Tests.Services;

public class MemoryInventoryParserServiceTests
{
    private readonly MemoryInventoryParserService _sut = new();

    [Fact]
    public void ParseSlots_FullModernPc_ExtractsBothDimms()
    {
        // 実フィクスチャ (ASUS EXPERTBOOK B1402CVA / SODIMM 2 枚 = 8GB×2 / DDR4-3200)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("MemoryInventory"), "full_modern_pc_slots.csv");

        var slots = _sut.ParseSlots(path);

        Assert.Equal(2, slots.Count);

        // 1 枚目 (Micron, PartNumber 空)
        Assert.Equal("BANK 0", slots[0].BankLabel);
        Assert.Equal("Controller0-ChannelA", slots[0].DeviceLocator);
        Assert.Equal(8, slots[0].CapacityGB);
        Assert.Equal(3200, slots[0].SpeedMhz);
        Assert.Equal(3200, slots[0].ConfiguredClockSpeedMhz);
        Assert.Equal(1200, slots[0].ConfiguredVoltageMv);
        Assert.Equal("Micron Technology", slots[0].Manufacturer);
        Assert.Equal(string.Empty, slots[0].PartNumber);
        Assert.Equal("00000000", slots[0].SerialNumber);
        Assert.Equal("SODIMM", slots[0].FormFactor);
        Assert.Equal("DDR4", slots[0].SmbiosMemoryType);
        Assert.Equal(64, slots[0].DataWidthBit);
        Assert.Equal(64, slots[0].TotalWidthBit);

        // 2 枚目 (SK Hynix, PartNumber あり)
        Assert.Equal("Controller1-ChannelA-DIMM0", slots[1].DeviceLocator);
        Assert.Equal("SK Hynix", slots[1].Manufacturer);
        Assert.Equal("HMAG68EXNSA051N", slots[1].PartNumber);
        Assert.Equal("77676B97", slots[1].SerialNumber);
    }

    [Fact]
    public void ParseSlots_FullModernPc_TotalCapacityMatches16GB()
    {
        // §29 sum = 装着総容量。Phase C で「総容量 (GB)」列に出す前提を保証。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("MemoryInventory"), "full_modern_pc_slots.csv");

        var slots = _sut.ParseSlots(path);
        var total = slots.Sum(s => s.CapacityGB ?? 0);

        Assert.Equal(16, total);
    }

    [Fact]
    public void ParseArraySummary_FullModernPc_ExtractsSingleArray()
    {
        // 実フィクスチャ (Physical Memory Array 0 / 最大 64GB / 装着 2 デバイス)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("MemoryInventory"), "full_modern_pc_array.csv");

        var arrays = _sut.ParseArraySummary(path);

        Assert.Single(arrays);
        Assert.Equal("Physical Memory Array 0", arrays[0].Tag);
        Assert.Equal("3", arrays[0].Location);     // WMI: 3 = System Board
        Assert.Equal("3", arrays[0].Use);          // WMI: 3 = System Memory
        Assert.Equal("3", arrays[0].MemoryErrorCorrection); // WMI: 3 = None
        Assert.Equal(64, arrays[0].MaxCapacityGB);
        Assert.Equal(2, arrays[0].MemoryDevices);
    }

    [Fact]
    public void ParseSlots_EmptyCsv_ReturnsEmptyList()
    {
        // ヘッダのみで行なし → 空コレクション (例外なし)
        var path = Path.Combine(TestPaths.GetTestDataDirectory("MemoryInventory"), "empty_slots.csv");

        var slots = _sut.ParseSlots(path);

        Assert.Empty(slots);
    }

    [Fact]
    public void ParseSlots_MissingFile_ReturnsEmptyList()
    {
        // ファイル不在時は例外を投げず空リスト (Dispatch 側の File.Exists ガードと整合)
        var slots = _sut.ParseSlots(Path.Combine(TestPaths.GetTestDataDirectory("MemoryInventory"), "does_not_exist.csv"));

        Assert.Empty(slots);
    }

    [Fact]
    public void ParseSlots_ReorderedColumns_HeaderIndexLookupResolves()
    {
        // CSV 列順が将来の evidence_config 改修で入れ替わっても header-index lookup で吸収する。
        // 列順位置依存パースを誤って導入した場合に検出する回帰テスト。
        var path = Path.Combine(TestPaths.GetTestDataDirectory("MemoryInventory"), "reordered_columns_slots.csv");

        var slots = _sut.ParseSlots(path);

        Assert.Single(slots);
        Assert.Equal("BANK 0", slots[0].BankLabel);
        Assert.Equal("DIMM_A1", slots[0].DeviceLocator);
        Assert.Equal(32, slots[0].CapacityGB);
        Assert.Equal("DDR5", slots[0].SmbiosMemoryType);
        Assert.Equal("DIMM", slots[0].FormFactor);
        Assert.Equal("Samsung", slots[0].Manufacturer);
        Assert.Equal("M323R4GA3BB0-CQK", slots[0].PartNumber);
        Assert.Equal(5200, slots[0].ConfiguredClockSpeedMhz);
        Assert.Equal(1100, slots[0].ConfiguredVoltageMv);
    }
}
