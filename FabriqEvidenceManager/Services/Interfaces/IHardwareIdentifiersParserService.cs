using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 31_HardwareIdentifiers.txt をパースし <see cref="HardwareIdentifiersData"/> に構造化する。
/// evidence_config v1.6.0+ §31 (kernel 3.0.0+) の 4 ブロック構成
/// (Win32_ComputerSystem / Win32_ComputerSystemProduct / Win32_BaseBoard / Win32_SystemEnclosure) を扱う。
/// </summary>
public interface IHardwareIdentifiersParserService
{
    /// <summary>
    /// 31_HardwareIdentifiers.txt をパースする。
    /// 各 probe は §31 仕様により inner try/catch で個別退避されるため、
    /// 取得失敗ブロックは対応プロパティが null で返る。
    /// </summary>
    /// <param name="filePath">31_HardwareIdentifiers.txt の絶対パス</param>
    /// <returns>抽出されたハードウェア識別子情報</returns>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    HardwareIdentifiersData Parse(string filePath);
}
