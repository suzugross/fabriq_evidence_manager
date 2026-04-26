using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 22_OfficeLicense.txt をパースし <see cref="OfficeLicenseData"/> に構造化する。
/// evidence_config.ps1 §22 の 2〜4 ブロック構成
/// (Click-to-Run config / OSPP path / OSPP /dstatus raw) を取り扱う。
/// </summary>
public interface IOfficeLicenseParserService
{
    /// <summary>
    /// 22_OfficeLicense.txt をパースする。
    /// </summary>
    /// <param name="filePath">22_OfficeLicense.txt の絶対パス</param>
    /// <returns>抽出された Office ライセンス情報</returns>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    OfficeLicenseData Parse(string filePath);
}
