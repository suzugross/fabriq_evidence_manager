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
}
