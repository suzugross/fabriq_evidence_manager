using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 23_SecurityBaseline.txt をパースし <see cref="SecurityBaselineData"/> に構造化する。
/// evidence_config.ps1 §23 (v1.4.0+) の 5 ブロック構成
/// (TPM / Secure Boot / VBS / LSA Protection / BIOS) を扱う。
/// </summary>
public interface ISecurityBaselineParserService
{
    /// <summary>
    /// 23_SecurityBaseline.txt をパースする。
    /// 各 probe は §23 仕様により inner try/catch で個別退避されるため、
    /// 取得失敗ブロックは対応プロパティが null で返る。
    /// </summary>
    /// <param name="filePath">23_SecurityBaseline.txt の絶対パス</param>
    /// <returns>抽出されたセキュリティベースライン情報</returns>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    SecurityBaselineData Parse(string filePath);
}
