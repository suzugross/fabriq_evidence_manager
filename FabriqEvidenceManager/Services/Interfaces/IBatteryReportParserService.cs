using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 26_BatteryReport.html をパースし <see cref="BatteryReportData"/> に構造化する。
/// evidence_config.ps1 §26 (v1.4.0+) が powercfg /batteryreport で生成する固定書式
/// HTML から DesignCapacity / FullChargeCapacity / CycleCount 等を抽出する。
/// </summary>
public interface IBatteryReportParserService
{
    /// <summary>
    /// 26_BatteryReport.html をパースする。
    /// Battery 非搭載 (デスクトップ) では HTML 自体が存在しないため、
    /// 呼び出し側 (EvidenceParserService) でファイル存在チェックを通過した
    /// ケースのみで使用される想定。
    /// パース失敗 (フィールド未検出) でも例外は投げず、検出できなかった
    /// 値は null のまま返す。
    /// </summary>
    /// <param name="filePath">26_BatteryReport.html の絶対パス</param>
    /// <returns>抽出されたバッテリ情報</returns>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    BatteryReportData Parse(string filePath);
}
