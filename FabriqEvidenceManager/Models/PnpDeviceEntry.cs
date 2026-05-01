namespace FabriqEvidenceManager.Models;

/// <summary>
/// 30_PnpDevices.csv の 1 行 = Plug and Play デバイス 1 件。
/// evidence_config v1.6.0+ §30 (kernel 3.0.0+) は Win32_PnPEntity 由来の全デバイス
/// (現在接続中 + 過去接続のキャッシュ含む) を出力する。1 PC あたり 200〜400 件規模。
/// 各列は raw 保持 (空欄も多い)。Class/FriendlyName が空のままの低レベルデバイスも含む。
/// </summary>
public sealed class PnpDeviceEntry
{
    /// <summary>デバイスクラス (例: "AudioEndpoint", "USB", "Display")。低レベルデバイスで空のことあり。</summary>
    public string Class { get; init; } = string.Empty;

    /// <summary>フレンドリ名 (例: "スピーカー (3- Realtek(R) Audio)")。空のこともある。</summary>
    public string FriendlyName { get; init; } = string.Empty;

    /// <summary>ステータス。"OK" / "Unknown" / "Error" / "Degraded" など。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>現在接続中フラグ。False は過去接続のキャッシュ (USB の取り外し履歴など)。</summary>
    public bool Present { get; init; }

    /// <summary>製造元 (例: "Microsoft", "Intel")。空のことあり。</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>ドライバサービス名。空のことあり。</summary>
    public string Service { get; init; } = string.Empty;

    /// <summary>ドライババージョン (例: "10.0.26100.1")。空のことあり。</summary>
    public string DriverVersion { get; init; } = string.Empty;

    /// <summary>ドライバ日付 (例: "2024-03-31")。raw 文字列保持。</summary>
    public string DriverDate { get; init; } = string.Empty;

    /// <summary>SMBIOS/USB/PCI 等のバス固有 ID。長文 (200 文字超もあり) で唯一性を持つ。</summary>
    public string InstanceId { get; init; } = string.Empty;
}
