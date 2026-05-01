using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 28_StartupItems.csv をパースする。
/// evidence_config v1.6.0+ §28 (kernel 3.0.0+) が出力する 6 列 CSV
/// (Source / Name / User / Command / Location / Enabled) を
/// レジストリ Run キー由来 + タスクスケジューラ由来の混在 1 リストにする。
/// </summary>
public interface IStartupItemsParserService
{
    /// <summary>
    /// 28_StartupItems.csv をパースする。
    /// CSV 列順は header-index lookup で吸収するため、将来順序が変わっても影響しない。
    /// </summary>
    /// <param name="filePath">28_StartupItems.csv の絶対パス</param>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    IReadOnlyList<StartupItemEntry> Parse(string filePath);
}
