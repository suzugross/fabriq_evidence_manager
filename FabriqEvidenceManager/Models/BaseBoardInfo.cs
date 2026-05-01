namespace FabriqEvidenceManager.Models;

/// <summary>
/// Win32_BaseBoard 由来 (evidence_config §31c)。マザーボードの SN は
/// §10 では canonical 候補から除外される (record-only) が、HW 監査で重要なため保持。
/// "(Win32_BaseBoard query failed: ...)" 時は
/// <see cref="HardwareIdentifiersData.BaseBoard"/> 自体が null になる。
/// </summary>
public sealed class BaseBoardInfo
{
    /// <summary>例: "ASUSTeK COMPUTER INC."</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>例: "B1402CVA"</summary>
    public string Product { get; init; } = string.Empty;

    /// <summary>例: "1.0"</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>マザーボード SN (record-only, §10 canonical からは除外)。例: "T206NXCV001M52MB"</summary>
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>例: "Base Board"</summary>
    public string Tag { get; init; } = string.Empty;
}
