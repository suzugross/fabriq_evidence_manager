using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// pc_information 内のログファイルおよび bitlocker ファイルをパースし、
/// シリアルナンバー・MACアドレス・BitLocker回復キー等の情報を抽出する。
/// </summary>
public interface IEvidenceParserService
{
    /// <summary>
    /// PcEvidence に紐づく各種ログファイルをパースし、
    /// シリアルナンバー・MACアドレス・BitLocker回復キーをモデルに充填する。
    /// </summary>
    /// <param name="pc">Discovery で構築済みの PcEvidence (パス情報が設定済みであること)</param>
    void PopulateDetails(PcEvidence pc);

    /// <summary>
    /// エビデンス欠損チェックを再実行し、HasWarning / WarningMessage を更新する。
    /// チェックボックス変更時に全PCを再検証するために使用する。
    /// </summary>
    /// <param name="pc">検証対象の PcEvidence</param>
    /// <param name="options">チェック項目の有効/無効設定</param>
    void RevalidateEvidence(PcEvidence pc, EvidenceCheckOptions options);

    /// <summary>
    /// 07_SerialNumber.txt からシリアルナンバーを抽出する。
    /// </summary>
    /// <param name="filePath">07_SerialNumber.txt の絶対パス</param>
    /// <returns>シリアルナンバー文字列。取得できない場合は空文字列。</returns>
    string ParseSerialNumber(string filePath);

    /// <summary>
    /// 06_MacAddress.txt からMAC情報一覧を抽出する。
    /// </summary>
    /// <param name="filePath">06_MacAddress.txt の絶対パス</param>
    /// <returns>検出された全NICのMAC情報リスト</returns>
    IReadOnlyList<MacAddressEntry> ParseMacAddresses(string filePath);

    /// <summary>
    /// bitlocker/{pcName}_{drive}.txt から回復キー情報を抽出する。
    /// </summary>
    /// <param name="filePath">BitLockerファイルの絶対パス</param>
    /// <returns>抽出されたBitLocker情報。パースに失敗した場合は null。</returns>
    BitLockerEntry? ParseBitLockerKey(string filePath);

    // ================================================================
    // hostlist 突合用パーサー
    // ================================================================

    /// <summary>
    /// 01_SystemInfo.txt からホスト名を抽出する。
    /// </summary>
    string ParseHostname(string filePath);

    /// <summary>
    /// 03_NetworkConfig.txt からネットワーク設定を抽出する。
    /// </summary>
    IReadOnlyList<NetworkInterfaceInfo> ParseNetworkConfig(string filePath);

    /// <summary>
    /// 04_Printers.txt からプリンタ一覧を抽出する。
    /// 行形式: Name=xxx|Driver=xxx|Port=xxx
    /// </summary>
    IReadOnlyList<PrinterEntry> ParsePrinters(string filePath);

    /// <summary>
    /// 05_BitLocker.txt からボリューム単位のBitLockerステータスを抽出する。
    /// </summary>
    IReadOnlyList<BitLockerVolumeStatus> ParseBitLockerStatus(string filePath);
}
