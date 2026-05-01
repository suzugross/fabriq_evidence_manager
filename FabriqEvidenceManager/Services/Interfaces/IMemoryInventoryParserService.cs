using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 29_MemorySlots.csv / 29b_MemoryArraySummary.csv をパースする。
/// evidence_config v1.6.0+ §29 (kernel 3.0.0+) は 1 セクションで 2 ファイル出力するため、
/// 1 サービスに 2 メソッドを集約する (§5 DomainStatus + UserProfiles と同様の方針)。
/// </summary>
public interface IMemoryInventoryParserService
{
    /// <summary>
    /// 29_MemorySlots.csv をパースする。
    /// 1 行 = 物理スロット 1 枚 (装着 DIMM)。空スロットは fabriq 側で行を出力しない仕様。
    /// </summary>
    /// <param name="filePath">29_MemorySlots.csv の絶対パス</param>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    IReadOnlyList<MemorySlotEntry> ParseSlots(string filePath);

    /// <summary>
    /// 29b_MemoryArraySummary.csv をパースする。
    /// シングル CPU 機では 1 件、デュアル CPU サーバでは複数件になりうる。
    /// </summary>
    /// <param name="filePath">29b_MemoryArraySummary.csv の絶対パス</param>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    IReadOnlyList<MemoryArraySummaryEntry> ParseArraySummary(string filePath);
}
