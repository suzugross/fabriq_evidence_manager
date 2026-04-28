using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 22_OfficeLicense.txt と 22_OfficeVnextLicenses.csv をパースし
/// <see cref="OfficeLicenseData"/> に構造化する。
/// evidence_config.ps1 §22 (v1.5.0+) の 4 ブロック構成
/// (22a Click-to-Run / 22b OSPP path+/dstatus / 22c vNext per-user / 22d INTERPRETATION) を扱う。
/// </summary>
public interface IOfficeLicenseParserService
{
    /// <summary>
    /// 22_OfficeLicense.txt と (任意で) 22_OfficeVnextLicenses.csv をパースする。
    /// </summary>
    /// <param name="txtFilePath">22_OfficeLicense.txt の絶対パス</param>
    /// <param name="vnextCsvPath">
    /// 22_OfficeVnextLicenses.csv の絶対パス。null または存在しないファイルの場合は
    /// <see cref="OfficeLicenseData.VnextEntries"/> が空配列になる
    /// (v1.4 以前の evidence や CSV 出力失敗時の対応)。
    /// </param>
    /// <returns>抽出された Office ライセンス情報</returns>
    /// <exception cref="System.IO.IOException">txt ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    OfficeLicenseData Parse(string txtFilePath, string? vnextCsvPath = null);
}
