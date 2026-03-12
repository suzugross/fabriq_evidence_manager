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

    /// <summary>魚拓 (スクリーンショット) 格納ディレクトリ</summary>
    public const string DirGyotaku = "gyotaku";

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

    /// <summary>ネットワーク設定ログ</summary>
    public const string FileNetworkConfig = "03_NetworkConfig.txt";

    /// <summary>プリンタ一覧ログ</summary>
    public const string FilePrinters = "04_Printers.txt";

    /// <summary>BitLockerステータスログ</summary>
    public const string FileBitLockerStatus = "05_BitLocker.txt";

    /// <summary>MACアドレスログ</summary>
    public const string FileMacAddress = "06_MacAddress.txt";

    /// <summary>シリアルナンバーログ</summary>
    public const string FileSerialNumber = "07_SerialNumber.txt";

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
