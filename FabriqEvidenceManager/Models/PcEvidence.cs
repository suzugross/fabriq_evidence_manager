using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 1台のPCに紐づくエビデンス情報の集約モデル。
/// DiscoveryService が evidence フォルダから構築し、各種 Service が利用する。
/// 自動リロード時の差分更新でUIへ変更通知を行うため ObservableObject を継承。
/// </summary>
public sealed partial class PcEvidence : ObservableObject
{
    /// <summary>PC名 (例: "NEW-PC-01")</summary>
    public required string PcName { get; init; }

    /// <summary>ハードウェアユニークID / シリアルナンバー (例: "T2NXCV06Y22208C")</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EthernetMac))]
    [NotifyPropertyChangedFor(nameof(WifiMac))]
    private string _serialNumber = string.Empty;

    /// <summary>収集日 (ディレクトリ名から取得, 例: "2026_02_26")</summary>
    public required string CollectionDate { get; init; }

    /// <summary>MACアドレス一覧 (イーサネット, Wi-Fi 等)</summary>
    public List<MacAddressEntry> MacAddresses { get; } = [];

    /// <summary>BitLocker回復キー一覧 (ドライブ別)</summary>
    public List<BitLockerEntry> BitLockerKeys { get; } = [];

    // ---- evidence ソースパス群 (ファイル収集機能で使用) ----

    /// <summary>pc_information ディレクトリの絶対パス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetectedItems))]
    private string? _pcInformationDirectoryPath;

    /// <summary>auto_capture ディレクトリの絶対パス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetectedItems))]
    private string? _autoCaptureDirectoryPath;

    /// <summary>bitlocker ディレクトリの絶対パス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetectedItems))]
    private string? _bitLockerDirectoryPath;

    /// <summary>checklist ファイルの絶対パス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetectedItems))]
    private string? _checklistFilePath;

    /// <summary>export_history ファイルの絶対パス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetectedItems))]
    private string? _exportHistoryFilePath;

    // ---- エビデンス欠損検知 ----

    /// <summary>何らかの欠損・異常がある場合に true</summary>
    [ObservableProperty]
    private bool _hasWarning;

    /// <summary>欠損項目の詳細メッセージ (カンマ区切り)</summary>
    [ObservableProperty]
    private string _warningMessage = string.Empty;

    // ---- 要確認項目検知 (hostlist不一致 / ベースラインNG / チェックリストNG) ----

    /// <summary>要確認項目がある場合に true (黄色ハイライト)</summary>
    [ObservableProperty]
    private bool _hasCaution;

    /// <summary>要確認項目の詳細メッセージ (カンマ区切り)</summary>
    [ObservableProperty]
    private string _cautionMessage = string.Empty;

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

    /// <summary>
    /// MAC/BitLocker 関連の算出プロパティの変更通知を発行する。
    /// MacAddresses / BitLockerKeys コレクション変更後に呼び出す。
    /// </summary>
    public void NotifyParsedDetailsChanged()
    {
        OnPropertyChanged(nameof(EthernetMac));
        OnPropertyChanged(nameof(WifiMac));
        OnPropertyChanged(nameof(HasBitLocker));
        OnPropertyChanged(nameof(BitLockerDrives));
    }
}
