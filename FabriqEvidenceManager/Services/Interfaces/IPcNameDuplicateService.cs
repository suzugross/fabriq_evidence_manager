using System.Collections.Generic;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 読み込み済み PC 全体から「計画 PC 名が同じ別端末」を検出する。
/// hostlist 突合は PC 名で引くため、同名 2 台は両方とも同じ行に一致してしまい検知できない。
/// その穴を埋めるため、PC 同士を突き合わせて重複を判定する。
/// </summary>
public interface IPcNameDuplicateService
{
    /// <summary>
    /// 重複を判定し、各 PC の <see cref="PcEvidence.DuplicatePcNameMachineCount"/> を更新する
    /// (重複していない PC は 0 にリセット)。
    /// <para>
    /// 判定ルール:
    ///  - 計画 PC 名は大文字小文字を区別しない (Windows のホスト名と同じ)。
    ///  - 同じシリアルの PC は同一端末の再実行とみなし、重複に数えない。
    ///  - シリアル不明の PC は同一端末か判別できないため、別端末として数える (見逃し防止)。
    /// </para>
    /// </summary>
    /// <returns>重複グループ一覧 (計画 PC 名順)。重複なしなら空。</returns>
    IReadOnlyList<PcNameDuplicateGroup> Evaluate(IReadOnlyList<PcEvidence> pcs);
}
