namespace FabriqEvidenceManager.Models;

/// <summary>
/// Win32_ComputerSystem 由来の機械プロファイル (evidence_config §31a)。
/// "(Win32_ComputerSystem query failed: ...)" 時は
/// <see cref="HardwareIdentifiersData.ComputerSystem"/> 自体が null になる。
/// </summary>
public sealed class ComputerSystemInfo
{
    /// <summary>例: "ASUSTeK COMPUTER INC."</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>例: "ASUS EXPERTBOOK B1402CVA_B1402CVA"</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>例: "ASUS EXPERTBOOK"</summary>
    public string SystemFamily { get; init; } = string.Empty;

    /// <summary>SMBIOS SystemSKUNumber (空のことが多い)</summary>
    public string SystemSKUNumber { get; init; } = string.Empty;

    /// <summary>raw 文字列保持。例: "15.63 GB"</summary>
    public string TotalPhysicalMem { get; init; } = string.Empty;

    /// <summary>物理 CPU 数。取得不能/未パース時は null。</summary>
    public int? NumberOfProcessors { get; init; }

    /// <summary>論理 CPU 数 (SMT 含む)。取得不能/未パース時は null。</summary>
    public int? NumberOfLogicalProcessors { get; init; }
}
