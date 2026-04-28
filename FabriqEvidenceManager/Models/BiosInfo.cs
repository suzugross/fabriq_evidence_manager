namespace FabriqEvidenceManager.Models;

/// <summary>
/// BIOS / Firmware 情報 (evidence_config §23e)。Win32_BIOS の出力に対応。
/// SerialNumber は §10 で別途収集されるため本クラスには含めない。
/// セクション全体が "(Win32_BIOS query failed: ...)" の場合は
/// <see cref="SecurityBaselineData.Bios"/> 自体が null になる。
/// </summary>
public sealed class BiosInfo
{
    /// <summary>例: "ASUSTeK COMPUTER INC. (Licensed by AMI, LLC.)"</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>例: "B1402CVA.313"</summary>
    public string SmbiosBiosVersion { get; init; } = string.Empty;

    /// <summary>raw 文字列保持。例: "02/06/2025 09:00:00"</summary>
    public string ReleaseDate { get; init; } = string.Empty;

    /// <summary>" / " 区切り結合済み (例: "_ASUS_ - 1072009 / B1402CVA.313 / American Megatrends - 5001B")</summary>
    public string BiosVersion { get; init; } = string.Empty;

    public int? SystemBiosMajor { get; init; }
    public int? SystemBiosMinor { get; init; }
    public int? SmbiosMajor { get; init; }
    public int? SmbiosMinor { get; init; }
}
