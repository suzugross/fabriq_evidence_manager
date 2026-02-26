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
}
