using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// パイロット実行のベースライン (export_history CSV) を管理するサービス。
/// </summary>
public interface IBaselineService
{
    /// <summary>ベースラインが読み込み済みかどうか</summary>
    bool IsLoaded { get; }

    /// <summary>
    /// ベースライン CSV を読み込む。
    /// </summary>
    /// <param name="csvFilePath">パイロットPCの export_history CSV ファイルパス</param>
    /// <returns>読み込んだモジュール数</returns>
    int Load(string csvFilePath);

    /// <summary>ベースラインをクリアする。</summary>
    void Clear();

    /// <summary>
    /// 対象PCの実行履歴をベースラインと突合する。
    /// </summary>
    /// <param name="pcName">対象PC名</param>
    /// <param name="actualHistory">対象PCの export_history エントリ一覧</param>
    /// <returns>突合レポート</returns>
    BaselineComparisonReport Compare(string pcName, IReadOnlyList<ExportHistoryEntry> actualHistory);
}
