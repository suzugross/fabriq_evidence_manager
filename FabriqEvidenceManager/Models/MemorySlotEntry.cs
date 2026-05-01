namespace FabriqEvidenceManager.Models;

/// <summary>
/// 29_MemorySlots.csv の 1 行 = 物理メモリスロット (DIMM/SODIMM 1 枚) の情報。
/// evidence_config v1.6.0+ §29 (kernel 3.0.0+) が出力する Win32_PhysicalMemory 由来。
/// 数値カラム (Capacity / Speed / Voltage / DataWidth) は int? で保持し、
/// パース不能な値や空文字列は null になる (将来サマリ算出での集計を想定)。
/// </summary>
public sealed class MemorySlotEntry
{
    /// <summary>例: "BANK 0" (SMBIOS BankLabel)</summary>
    public string BankLabel { get; init; } = string.Empty;

    /// <summary>例: "Controller0-ChannelA" (SMBIOS DeviceLocator)</summary>
    public string DeviceLocator { get; init; } = string.Empty;

    /// <summary>装着 DIMM 容量 (GB)。空 / パース不能時 null。</summary>
    public int? CapacityGB { get; init; }

    /// <summary>定格速度 (MHz)。例: 3200。</summary>
    public int? SpeedMhz { get; init; }

    /// <summary>実動作速度 (MHz)。BIOS 設定により Speed と異なる場合あり。</summary>
    public int? ConfiguredClockSpeedMhz { get; init; }

    /// <summary>実動作電圧 (mV)。例: 1200 (DDR4 標準)。</summary>
    public int? ConfiguredVoltageMv { get; init; }

    /// <summary>例: "Micron Technology"</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>例: "HMAG68EXNSA051N" (空のことあり: ASUS BIOS の OEM 機など)</summary>
    public string PartNumber { get; init; } = string.Empty;

    /// <summary>例: "77676B97" (8 桁 hex / "00000000" は SN 取得不能)</summary>
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>例: "SODIMM" / "DIMM" (Win32_PhysicalMemory.FormFactor 値)</summary>
    public string FormFactor { get; init; } = string.Empty;

    /// <summary>例: "DDR4" / "DDR5" (Win32_PhysicalMemory.SMBIOSMemoryType の text 化結果)</summary>
    public string SmbiosMemoryType { get; init; } = string.Empty;

    /// <summary>データ幅 (bit)。ECC 有無の audit にも使用。</summary>
    public int? DataWidthBit { get; init; }

    /// <summary>合計幅 (bit)。Data &lt; Total なら ECC bit あり。</summary>
    public int? TotalWidthBit { get; init; }
}
