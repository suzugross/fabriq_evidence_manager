namespace FabriqEvidenceManager.Models;

/// <summary>
/// Win32_ComputerSystemProduct 由来 (evidence_config §31b)。
/// IdentifyingNumber は §10 SerialNumber の canonical candidate でもあり、
/// SerialNumberSource = "Win32_ComputerSystemProduct.IdentifyingNumber" 採用時の audit 突合用。
/// "(Win32_ComputerSystemProduct query failed: ...)" 時は
/// <see cref="HardwareIdentifiersData.ComputerSystemProduct"/> 自体が null になる。
/// </summary>
public sealed class ComputerSystemProductInfo
{
    /// <summary>例: "ASUSTeK COMPUTER INC."</summary>
    public string Vendor { get; init; } = string.Empty;

    /// <summary>例: "ASUS EXPERTBOOK B1402CVA_B1402CVA"</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>例: "1.0"</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>SN 候補値の 1 つ。例: "T2NXCV06Y22208C"</summary>
    public string IdentifyingNumber { get; init; } = string.Empty;

    /// <summary>SMBIOS UUID。例: "99ACFEAA-AB7C-8C4D-8E4D-35A66EAEFE05"</summary>
    public string Uuid { get; init; } = string.Empty;

    /// <summary>SMBIOS SKUNumber (空のことが多い)</summary>
    public string SkuNumber { get; init; } = string.Empty;
}
