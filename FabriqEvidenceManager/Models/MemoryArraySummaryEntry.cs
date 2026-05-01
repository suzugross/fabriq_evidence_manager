namespace FabriqEvidenceManager.Models;

/// <summary>
/// 29b_MemoryArraySummary.csv の 1 行 = 物理メモリアレイ (マザーボード上の
/// メモリコントローラ単位) のサマリ。evidence_config v1.6.0+ §29 が出力する
/// Win32_PhysicalMemoryArray 由来。シングル CPU 機では通常 1 行、
/// デュアル CPU サーバでは複数行になりうる (List 保持)。
/// </summary>
public sealed class MemoryArraySummaryEntry
{
    /// <summary>例: "Physical Memory Array 0"</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>WMI Location 数値コード (raw 保持。例: "3" = System Board)</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>WMI Use 数値コード (raw 保持。例: "3" = System Memory)</summary>
    public string Use { get; init; } = string.Empty;

    /// <summary>WMI MemoryErrorCorrection 数値コード (raw 保持。例: "3" = None)</summary>
    public string MemoryErrorCorrection { get; init; } = string.Empty;

    /// <summary>マザーボード設計上の最大搭載可能容量 (GB)。装着量ではない。</summary>
    public int? MaxCapacityGB { get; init; }

    /// <summary>このアレイに紐づくスロット数 (装着 + 空きの合計)。</summary>
    public int? MemoryDevices { get; init; }
}
