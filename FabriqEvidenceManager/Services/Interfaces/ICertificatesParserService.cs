using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 25_Certificates.csv をパースし <see cref="CertificateEntry"/> リストに変換する。
/// evidence_config.ps1 §25 (v1.4.0+) の 4 ストア統合 CSV (10 列) を扱う。
/// </summary>
public interface ICertificatesParserService
{
    /// <summary>
    /// 25_Certificates.csv をパースする。
    /// ヘッダのみ / 不在ファイル / 空行は無視する。
    /// </summary>
    /// <param name="filePath">25_Certificates.csv の絶対パス</param>
    /// <returns>証明書一覧 (空可)</returns>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    IReadOnlyList<CertificateEntry> Parse(string filePath);
}
