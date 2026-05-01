using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// 24_GroupPolicySummary.txt をパースして <see cref="GroupPolicyReport"/> に構造化する。
/// HTML 本体 (24_GroupPolicy.html) は読み込まず、ファイルパスのみ Model に格納する設計。
/// evidence_config v1.6.0+ §24 (kernel 3.0.0+) 対応。
/// </summary>
public interface IGroupPolicyParserService
{
    /// <summary>
    /// gpresult サマリ TXT をパースし、HTML パスと併せて構造化レポートを返す。
    /// </summary>
    /// <param name="summaryFilePath">24_GroupPolicySummary.txt の絶対パス</param>
    /// <param name="htmlFilePath">24_GroupPolicy.html の絶対パス。HTML 不在時は null</param>
    /// <exception cref="System.IO.IOException">ファイル読み込み失敗</exception>
    /// <exception cref="System.UnauthorizedAccessException">アクセス権限エラー</exception>
    GroupPolicyReport Parse(string summaryFilePath, string? htmlFilePath);
}
