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
    /// <summary>
    /// 計画 PC 名 (例: "NEW-PC-01")。
    /// ディレクトリ名から取得 (= manifest.selectedNewPcName = $env:SELECTED_NEW_PCNAME)。
    /// 「rename したかった名前」を表す。
    /// </summary>
    public required string PcName { get; init; }

    /// <summary>
    /// 実 PC 名 (manifest.computerName = $env:COMPUTERNAME)。
    /// 「実際に OS が認識している現在の PC 名」を表す。
    /// PcName と異なれば rename 失敗の可能性あり (HasPcNameMismatch=true)。
    /// manifest 不在時は null。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPcNameMismatch))]
    [NotifyPropertyChangedFor(nameof(PcNameDisplay))]
    [NotifyPropertyChangedFor(nameof(ActualComputerNameDisplay))]
    private string? _actualComputerName;

    /// <summary>計画 PC 名と実 PC 名が一致しない (rename 失敗または不整合)。</summary>
    public bool HasPcNameMismatch =>
        !string.IsNullOrEmpty(ActualComputerName)
        && !string.Equals(PcName, ActualComputerName, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>UI 表示用の実 PC 名 (null/空 のときは "(実名不明)" を返す)。</summary>
    public string ActualComputerNameDisplay =>
        string.IsNullOrEmpty(ActualComputerName) ? "(実名不明)" : ActualComputerName;

    /// <summary>
    /// 計画名と実名を併記した表示用文字列 (狭い表示エリア用)。
    /// 形式: "計画 | 実" (一致時) / "計画 | 実 ⚠" (不一致時) / "計画 | (実名不明)" (manifest 不在時)。
    /// </summary>
    public string PcNameDisplay =>
        string.IsNullOrEmpty(ActualComputerName)
            ? $"{PcName} | (実名不明)"
            : HasPcNameMismatch
                ? $"{PcName} | {ActualComputerName} ⚠"
                : $"{PcName} | {ActualComputerName}";

    /// <summary>ハードウェアユニークID / シリアルナンバー (例: "T2NXCV06Y22208C")</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EthernetMac))]
    [NotifyPropertyChangedFor(nameof(WifiMac))]
    [NotifyPropertyChangedFor(nameof(HasSerialFallbackWarning))]
    [NotifyPropertyChangedFor(nameof(SerialNumberDisplay))]
    private string _serialNumber = string.Empty;

    /// <summary>
    /// SN を採用したソースラベル (例: "Win32_BIOS.SerialNumber")。
    /// Primary = Win32_BIOS.SerialNumber のみ。それ以外はフォールバックとして警告対象。
    /// 取得不能 / manifest なし時は null。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSerialFromPrimarySource))]
    [NotifyPropertyChangedFor(nameof(HasSerialFallbackWarning))]
    [NotifyPropertyChangedFor(nameof(SerialNumberDisplay))]
    private string? _serialNumberSource;

    /// <summary>Win32_ComputerSystemProduct.UUID (audit reference, SN ではない)</summary>
    [ObservableProperty]
    private string _serialReferenceUuid = string.Empty;

    /// <summary>SN 取得試行の全ソース履歴 (詳細パネル用)</summary>
    public List<SerialSourceEntry> SerialSourceTrail { get; } = [];

    /// <summary>採用ソースが Primary canonical (Win32_BIOS.SerialNumber) かどうか</summary>
    public bool IsSerialFromPrimarySource =>
        SerialNumberSource == SerialSourceEntry.PrimaryCanonicalLabel;

    /// <summary>SN がフォールバック取得 (BIOS 系以外) で audit 警告対象</summary>
    public bool HasSerialFallbackWarning =>
        !string.IsNullOrEmpty(SerialNumber) && !IsSerialFromPrimarySource;

    /// <summary>
    /// UI/Excel の狭幅表示用文字列。
    /// Primary 一致: "ABC123" / フォールバック: "ABC123 ⚠ (CSProduct)" / 取得不能: "(取得不能)"。
    /// </summary>
    public string SerialNumberDisplay =>
        string.IsNullOrEmpty(SerialNumber)
            ? "(取得不能)"
            : IsSerialFromPrimarySource
                ? SerialNumber
                : $"{SerialNumber} ⚠ ({SerialSourceEntry.ShortLabel(SerialNumberSource)})";

    /// <summary>収集日 (ディレクトリ名から取得, 例: "2026_02_26")</summary>
    public required string CollectionDate { get; init; }

    /// <summary>MACアドレス一覧 (イーサネット, Wi-Fi 等)</summary>
    public List<MacAddressEntry> MacAddresses { get; } = [];

    /// <summary>BitLocker回復キー一覧 (ドライブ別)</summary>
    public List<BitLockerEntry> BitLockerKeys { get; } = [];

    /// <summary>ローカルユーザーアカウント一覧</summary>
    public List<LocalUserEntry> LocalUsers { get; } = [];

    /// <summary>ローカルグループ一覧</summary>
    public List<LocalGroupEntry> LocalGroups { get; } = [];

    /// <summary>ローカルグループメンバー一覧</summary>
    public List<LocalGroupMemberEntry> LocalGroupMembers { get; } = [];

    /// <summary>ドメイン参加状態</summary>
    public DomainStatusData? DomainStatus { get; set; }

    /// <summary>ファイアウォールプロファイル設定</summary>
    public List<FirewallProfileEntry> FirewallProfiles { get; } = [];

    /// <summary>ファイアウォールルール一覧</summary>
    public List<FirewallRuleEntry> FirewallRules { get; } = [];

    /// <summary>Windowsオプション機能一覧</summary>
    public List<OptionalFeatureEntry> OptionalFeatures { get; } = [];

    /// <summary>ユーザープロファイル一覧</summary>
    public List<UserProfileEntry> UserProfiles { get; } = [];

    /// <summary>物理ディスク一覧</summary>
    public List<DiskEntry> Disks { get; } = [];

    /// <summary>パーティション一覧</summary>
    public List<PartitionEntry> Partitions { get; } = [];

    /// <summary>電源設定</summary>
    public PowerSettingsData? PowerSettings { get; set; }

    /// <summary>WiFiプロファイル名一覧</summary>
    public List<string> WiFiProfiles { get; } = [];

    /// <summary>復元ポイント一覧</summary>
    public List<RestorePointEntry> RestorePoints { get; } = [];

    /// <summary>Windows Defender 状態</summary>
    public DefenderStatusData? DefenderStatus { get; set; }

    /// <summary>Windows Update 一覧</summary>
    public List<WindowsUpdateEntry> WindowsUpdates { get; } = [];

    /// <summary>
    /// 未知 manifest section の raw データ。
    /// EvidenceConstants.KnownSections に無い ID のセクションがここに集約される
    /// (M5: 前方互換、§23 等の将来追加に対応)。
    /// </summary>
    public List<UnknownSection> UnknownSections { get; } = [];

    /// <summary>Windows ライセンス / アクティベーション状態 (evidence_config v1.3.0+ §21)</summary>
    [ObservableProperty]
    private WindowsLicenseData? _windowsLicense;

    /// <summary>Office ライセンス / アクティベーション状態 (evidence_config v1.3.0+ §22)</summary>
    [ObservableProperty]
    private OfficeLicenseData? _officeLicense;

    /// <summary>
    /// pc_information/manifest.json のパース結果 (kernel/EVIDENCE_MANIFEST.md schemaVersion=1)。
    /// null の場合は <see cref="ManifestError"/> に理由が入る。
    /// </summary>
    [ObservableProperty]
    private EvidenceManifest? _manifest;

    /// <summary>
    /// manifest.json 読み込み失敗時のエラーメッセージ。
    /// null の場合は manifest が正常に読めた、または PC 情報ディレクトリ自体が無い。
    /// </summary>
    [ObservableProperty]
    private string? _manifestError;

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
