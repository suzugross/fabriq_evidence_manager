using System.Collections.Generic;
using System.Linq;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 1台のPCに紐づくエビデンス情報の集約モデル。
/// DiscoveryService が evidence フォルダから構築し、各種 Service が利用する。
/// </summary>
public sealed class PcEvidence
{
    /// <summary>PC名 (例: "NEW-PC-01")</summary>
    public required string PcName { get; init; }

    /// <summary>ハードウェアユニークID / シリアルナンバー (例: "T2NXCV06Y22208C")</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>収集日 (ディレクトリ名から取得, 例: "2026_02_26")</summary>
    public required string CollectionDate { get; init; }

    /// <summary>MACアドレス一覧 (イーサネット, Wi-Fi 等)</summary>
    public List<MacAddressEntry> MacAddresses { get; } = [];

    /// <summary>BitLocker回復キー一覧 (ドライブ別)</summary>
    public List<BitLockerEntry> BitLockerKeys { get; } = [];

    // ---- evidence ソースパス群 (ファイル収集機能で使用) ----

    /// <summary>pc_information ディレクトリの絶対パス</summary>
    public string? PcInformationDirectoryPath { get; set; }

    /// <summary>auto_capture ディレクトリの絶対パス</summary>
    public string? AutoCaptureDirectoryPath { get; set; }

    /// <summary>bitlocker ディレクトリの絶対パス</summary>
    public string? BitLockerDirectoryPath { get; set; }

    /// <summary>checklist ファイルの絶対パス</summary>
    public string? ChecklistFilePath { get; set; }

    /// <summary>export_history ファイルの絶対パス</summary>
    public string? ExportHistoryFilePath { get; set; }

    // ---- DataGrid 表示用の算出プロパティ ----

    /// <summary>イーサネットのMACアドレス (未検出時は空文字列)</summary>
    public string EthernetMac =>
        MacAddresses.FirstOrDefault(m =>
            m.ConnectionName.Contains("イーサネット", StringComparison.OrdinalIgnoreCase)
            || m.ConnectionName.Contains("Ethernet", StringComparison.OrdinalIgnoreCase))
        ?.PhysicalAddress ?? string.Empty;

    /// <summary>Wi-FiのMACアドレス (未検出時は空文字列)</summary>
    public string WifiMac =>
        MacAddresses.FirstOrDefault(m =>
            m.ConnectionName.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
            || m.ConnectionName.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
            || m.ConnectionName.Contains("ワイヤレス", StringComparison.OrdinalIgnoreCase))
        ?.PhysicalAddress ?? string.Empty;

    /// <summary>BitLocker回復キーの有無</summary>
    public bool HasBitLocker => BitLockerKeys.Count > 0;

    /// <summary>BitLocker概要 (例: "C, D")</summary>
    public string BitLockerDrives =>
        BitLockerKeys.Count > 0
            ? string.Join(", ", BitLockerKeys.Select(k => k.DriveLetter + ":"))
            : string.Empty;

    /// <summary>エビデンス検出状況 (pc_info / capture / checklist / history)</summary>
    public string DetectedItems =>
        string.Join(" / ",
            new[] {
                PcInformationDirectoryPath is not null ? "PC情報" : null,
                AutoCaptureDirectoryPath is not null ? "キャプチャ" : null,
                BitLockerDirectoryPath is not null ? "BitLocker" : null,
                ChecklistFilePath is not null ? "チェックリスト" : null,
                ExportHistoryFilePath is not null ? "履歴" : null,
            }.Where(s => s is not null));
}
