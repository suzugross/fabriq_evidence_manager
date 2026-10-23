using System.Collections.Generic;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 計画 PC 名が別端末と重複している PC のグループ
/// (誤って同じホスト名を 2 台以上に設定したケース)。
/// </summary>
public sealed class PcNameDuplicateGroup
{
    /// <summary>重複している計画 PC 名 (グループ内先頭 PC の表記)</summary>
    public required string PcName { get; init; }

    /// <summary>別端末と判定した台数 (2 以上)。同一端末の再実行は 1 台として数える。</summary>
    public required int MachineCount { get; init; }

    /// <summary>グループに属する PC (同一端末の再実行分も含む)</summary>
    public required IReadOnlyList<PcEvidence> Members { get; init; }

    /// <summary>表示用サマリ (例: "PC-01 (別端末 2 台)")</summary>
    public string Summary => $"{PcName} (別端末 {MachineCount} 台)";
}
