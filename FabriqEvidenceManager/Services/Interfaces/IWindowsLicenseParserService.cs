using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 21_WindowsLicense.txt をパースし <see cref="WindowsLicenseData"/> に構造化する。
/// evidence_config.ps1 §21 が出力する 3 ブロック構成
/// (SoftwareLicensingProduct / SoftwareLicensingService / slmgr /dlv raw) を
/// すべて取り扱う。
/// </summary>
public interface IWindowsLicenseParserService
{
    /// <summary>
    /// 21_WindowsLicense.txt をパースする。
    /// </summary>
    /// <param name="filePath">21_WindowsLicense.txt の絶対パス</param>
    /// <returns>抽出された Windows ライセンス情報</returns>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    WindowsLicenseData Parse(string filePath);
}
