using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 30_PnpDevices.csv をパースする。
/// evidence_config v1.6.0+ §30 (kernel 3.0.0+) が出力する 9 列 CSV
/// (Class / FriendlyName / Status / Present / Manufacturer / Service /
///  DriverVersion / DriverDate / InstanceId) を 1 リストにする。
/// 1 PC あたり 200〜400 件規模で行数が多いため、列順非依存パースが必須。
/// </summary>
public interface IPnpDevicesParserService
{
    /// <summary>
    /// 30_PnpDevices.csv をパースする。
    /// CSV 列順は header-index lookup で吸収するため、将来順序が変わっても影響しない。
    /// </summary>
    /// <param name="filePath">30_PnpDevices.csv の絶対パス</param>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    IReadOnlyList<PnpDeviceEntry> Parse(string filePath);
}
