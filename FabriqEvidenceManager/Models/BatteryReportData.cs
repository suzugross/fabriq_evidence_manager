using System;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 26_BatteryReport.html (powercfg /batteryreport) のパース結果。
/// evidence_config.ps1 §26 (v1.4.0+) が出力する HTML の "Installed batteries"
/// セクションから取得。Battery 非搭載 (デスクトップ) の場合 §26 は Skipped となり
/// HTML 自体が存在しない (本クラスはインスタンス化されない)。
/// </summary>
public sealed class BatteryReportData
{
    /// <summary>例: "X421-35"</summary>
    public string Name { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>例: "Li" / "LION"</summary>
    public string Chemistry { get; init; } = string.Empty;

    /// <summary>設計容量 (mWh)。parse 失敗時 null。</summary>
    public int? DesignCapacityMwh { get; init; }

    /// <summary>満充電時実容量 (mWh)。parse 失敗時 null。</summary>
    public int? FullChargeCapacityMwh { get; init; }

    /// <summary>サイクル数。parse 失敗時 null。</summary>
    public int? CycleCount { get; init; }

    /// <summary>
    /// 健全性 % = FullChargeCapacity / DesignCapacity × 100 (小数 1 位四捨五入)。
    /// いずれかの容量が null / DesignCapacity が 0 以下の場合 null。
    /// </summary>
    public double? HealthPercent
    {
        get
        {
            if (DesignCapacityMwh is null || FullChargeCapacityMwh is null) return null;
            if (DesignCapacityMwh.Value <= 0) return null;
            return Math.Round(100.0 * FullChargeCapacityMwh.Value / DesignCapacityMwh.Value, 1);
        }
    }
}
