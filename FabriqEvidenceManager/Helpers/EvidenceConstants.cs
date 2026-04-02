namespace FabriqEvidenceManager.Helpers;

/// <summary>
/// fabriq evidence 構造に関する定数を一元管理する。
/// ディレクトリ名、ファイル名、ファイルプレフィックス、出力命名規則など、
/// プロジェクト全体で参照される固定文字列をここに集約し DRY 原則を担保する。
/// </summary>
public static class EvidenceConstants
{
    // ================================================================
    // evidence サブディレクトリ名
    // ================================================================

    /// <summary>PC情報ログ格納ディレクトリ</summary>
    public const string DirPcInformation = "pc_information";

    /// <summary>自動キャプチャ画像格納ディレクトリ</summary>
    public const string DirAutoCapture = "auto_capture";

    /// <summary>BitLocker回復キー格納ディレクトリ</summary>
    public const string DirBitLocker = "bitlocker";

    /// <summary>チェックリスト格納ディレクトリ</summary>
    public const string DirChecklist = "checklist";

    /// <summary>エクスポート履歴格納ディレクトリ</summary>
    public const string DirExportHistory = "export_history";

    // ================================================================
    // ファイルプレフィックス (checklist, export_history のファイル名先頭)
    // ================================================================

    /// <summary>チェックリストファイルのプレフィックス</summary>
    public const string PrefixChecklist = "checklist_";

    /// <summary>エクスポート履歴ファイルのプレフィックス</summary>
    public const string PrefixExportHistory = "history_export_";

    // ================================================================
    // pc_information 内の個別ログファイル名
    // ================================================================

    /// <summary>システム基本情報ログ</summary>
    public const string FileSystemInfo = "01_SystemInfo.txt";

    /// <summary>ネットワーク設定CSV</summary>
    public const string FileNetworkConfig = "06_NetworkConfig.csv";

    /// <summary>プリンタ一覧CSV</summary>
    public const string FilePrinters = "07_Printers.csv";

    /// <summary>BitLockerステータスログ</summary>
    public const string FileBitLockerStatus = "08_BitLocker.txt";

    /// <summary>MACアドレスCSV</summary>
    public const string FileMacAddress = "09_MacAddress.csv";

    /// <summary>シリアルナンバーログ</summary>
    public const string FileSerialNumber = "10_SerialNumber.txt";

    /// <summary>デスクトップアプリ一覧CSV</summary>
    public const string FileDesktopApps = "11_DesktopApps.csv";

    /// <summary>ストアアプリ一覧CSV</summary>
    public const string FileStoreApps = "11_StoreApps.csv";

    /// <summary>ローカルユーザー一覧CSV</summary>
    public const string FileLocalUsers = "02_LocalUsers.csv";

    /// <summary>ローカルグループ一覧CSV</summary>
    public const string FileLocalGroups = "03_LocalGroups.csv";

    /// <summary>ローカルグループメンバーCSV</summary>
    public const string FileLocalGroupMembers = "04_LocalGroupMembers.csv";

    /// <summary>ドメイン参加状態ログ</summary>
    public const string FileDomainStatus = "05_DomainStatus.txt";

    /// <summary>ファイアウォールプロファイルCSV</summary>
    public const string FileFirewallProfiles = "12_FirewallProfiles.csv";

    /// <summary>ファイアウォールルールCSV</summary>
    public const string FileFirewallRules = "12_FirewallRules.csv";

    /// <summary>Windowsオプション機能CSV</summary>
    public const string FileOptionalFeatures = "13_OptionalFeatures.csv";

    /// <summary>ユーザープロファイルCSV</summary>
    public const string FileUserProfiles = "05_UserProfiles.csv";

    /// <summary>物理ディスク情報CSV</summary>
    public const string FileDisks = "08b_Disks.csv";

    /// <summary>パーティション情報CSV</summary>
    public const string FilePartitions = "08b_Partitions.csv";

    /// <summary>電源設定ログ</summary>
    public const string FilePowerSettings = "15_PowerSettings.txt";

    /// <summary>WiFiプロファイルログ</summary>
    public const string FileWiFiProfiles = "16_WiFiProfiles.txt";

    /// <summary>復元ポイントCSV</summary>
    public const string FileRestorePoints = "17_RestorePoints.csv";

    /// <summary>Windows Defenderステータスログ</summary>
    public const string FileDefenderStatus = "18_DefenderStatus.txt";

    /// <summary>Windows Update一覧CSV</summary>
    public const string FileWindowsUpdates = "19_WindowsUpdates.csv";

    // ================================================================
    // 出力ファイル / ディレクトリ命名
    // ================================================================

    /// <summary>納品データ出力フォルダのサフィックス (日付部分は呼び出し元で付与)</summary>
    public const string DeliveryDirSuffix = "fabriq_evi";

    /// <summary>PC情報一覧表Excelのファイル名サフィックス (タイムスタンプ部分は呼び出し元で付与)</summary>
    public const string ExcelFileNameSuffix = "PC情報一覧表";

    /// <summary>Excelシート名</summary>
    public const string ExcelSheetName = "PC情報一覧";

    // ================================================================
    // Excel 書式
    // ================================================================

    /// <summary>列幅自動調整の上限値</summary>
    public const double ExcelMaxColumnWidth = 55;
}
