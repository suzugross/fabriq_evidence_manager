using System;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// Excel エクスポート時に Sheet 1 (PC情報一覧) の冒頭 6 行に出力する案件メタデータ。
/// fabriqKernelVersion / evidenceConfigVersion / PC 台数 / 突合 OK・NG 件数は
/// pcEvidences と manifest から自動算出されるため本型では持たない。
/// </summary>
public sealed class ExcelDeliveryMetadata
{
    /// <summary>案件名</summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>顧客名</summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>作業者名</summary>
    public string WorkerName { get; init; } = string.Empty;

    /// <summary>作業日</summary>
    public DateTime? WorkDate { get; init; }
}
