using CommunityToolkit.Mvvm.ComponentModel;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// エビデンス欠損チェックの各項目の有効/無効を保持するオプション。
/// チェックボックスとバインドし、OFF の項目は欠損判定をスキップする。
/// </summary>
public sealed partial class EvidenceCheckOptions : ObservableObject
{
    /// <summary>MACアドレス(有線) の取得チェック</summary>
    [ObservableProperty]
    private bool _checkEthernetMac = true;

    /// <summary>MACアドレス(Wi-Fi) の取得チェック</summary>
    [ObservableProperty]
    private bool _checkWifiMac = true;

    /// <summary>BitLocker回復キー の取得チェック</summary>
    [ObservableProperty]
    private bool _checkBitLocker = true;

    /// <summary>pc_information ディレクトリの存在チェック</summary>
    [ObservableProperty]
    private bool _checkPcInformation = true;

    /// <summary>auto_capture ディレクトリの存在チェック</summary>
    [ObservableProperty]
    private bool _checkAutoCapture = true;

    /// <summary>チェックリストファイルの存在チェック</summary>
    [ObservableProperty]
    private bool _checkChecklist = true;

    /// <summary>余剰プリンタ検出 (hostlistにないプリンタをNG扱い)</summary>
    [ObservableProperty]
    private bool _checkExtraPrinters = false;

    /// <summary>
    /// Windows ライセンス認証チェック。
    /// ON のとき WindowsLicense 未取得 / 未認証 (LicenseStatus≠1) のいずれも警告。
    /// </summary>
    [ObservableProperty]
    private bool _checkWindowsLicense = true;

    /// <summary>
    /// Office インストール有無チェック。
    /// ON のとき OfficeLicense 未取得 / 未インストールが警告。
    /// 「Office を必須としない案件」では OFF にする。
    /// </summary>
    [ObservableProperty]
    private bool _checkOfficeInstalled = true;

    /// <summary>
    /// Office ライセンス認証チェック (インストール済みの場合のみ意味を持つ)。
    /// ON のとき manifest §22 verdict=Failed → 警告 (赤)、Partial → 要確認 (オレンジ)。
    /// 「インストールはあるが認証は不要」案件では OFF にする。
    /// </summary>
    [ObservableProperty]
    private bool _checkOfficeLicense = true;
}
