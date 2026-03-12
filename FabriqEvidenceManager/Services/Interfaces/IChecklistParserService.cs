using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// チェックリストHTML と実行履歴CSV のパースを提供するサービス。
/// </summary>
public interface IChecklistParserService
{
    /// <summary>
    /// チェックリスト HTML をパースして結果を返す。
    /// </summary>
    ChecklistResult ParseChecklistHtml(string filePath);

    /// <summary>
    /// export_history CSV をパースして実行履歴一覧を返す。
    /// </summary>
    IReadOnlyList<ExportHistoryEntry> ParseExportHistory(string filePath);
}
