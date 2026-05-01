namespace FabriqEvidenceManager.Models;

/// <summary>
/// Win32_SystemEnclosure 由来 (evidence_config §31d)。
/// SerialNumber は §10 SerialNumber の canonical candidate
/// "Win32_SystemEnclosure.SerialNumber" としても利用される。
/// "(Win32_SystemEnclosure query failed: ...)" 時は
/// <see cref="HardwareIdentifiersData.SystemEnclosure"/> 自体が null になる。
/// </summary>
public sealed class SystemEnclosureInfo
{
    /// <summary>例: "ASUSTeK COMPUTER INC."</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>機種モデル名 (空のことが多い)</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>raw 文字列保持。例: "10 (Notebook)" — コードと表記が併記される。</summary>
    public string ChassisTypes { get; init; } = string.Empty;

    /// <summary>SN 候補値の 1 つ。例: "T2NXCV06Y22208C"</summary>
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>SMBIOS Asset Tag。例: "No Asset Tag"</summary>
    public string SmbiosAssetTag { get; init; } = string.Empty;

    /// <summary>運用 Asset Tag (空のことが多い)</summary>
    public string AssetTag { get; init; } = string.Empty;

    /// <summary>SMBIOS SecurityStatus 数値コード。3=None。取得不能/未パース時は null。</summary>
    public int? SecurityStatus { get; init; }

    /// <summary>物理ロック有無。取得不能/未パース時は null。</summary>
    public bool? LockPresent { get; init; }
}
