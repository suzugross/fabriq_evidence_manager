using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// パイロット実行のベースライン管理サービス。
/// PC一覧から選択したベースラインPCのエビデンス全体を読み込み、
/// 実行サマリ・SystemInfo・チェックリストの照合を一括で行う。
/// </summary>
public interface IBaselineService
{
    /// <summary>ベースラインが読み込み済みかどうか</summary>
    bool IsLoaded { get; }

    /// <summary>ベースラインPC名 (未設定時は null)</summary>
    string? BaselinePcName { get; }

    /// <summary>登録済みコンパレータのメタデータ一覧 (設定 UI で使用)</summary>
    IReadOnlyList<BaselineCategoryInfo> AvailableCategories { get; }

    /// <summary>現在 CompareAll で実行されるカテゴリ ID のセット</summary>
    IReadOnlyCollection<string> EnabledCategoryIds { get; }

    /// <summary>
    /// CompareAll の対象カテゴリを指定する。
    /// ここに含まれない CategoryId のコンパレータは <see cref="CompareAll"/> でスキップされる
    /// (キャッシュ自体は LoadFromPc で保持されている)。
    /// </summary>
    void SetEnabledCategoryIds(IEnumerable<string> ids);

    /// <summary>
    /// ベースライン CSV を読み込む。
    /// </summary>
    /// <param name="csvFilePath">パイロットPCの export_history CSV ファイルパス</param>
    /// <returns>読み込んだモジュール数</returns>
    int Load(string csvFilePath);

    /// <summary>
    /// ベースラインPCを丸ごと設定する。
    /// PC一覧から選択したPCの export_history CSV / SystemInfo / チェックリストを一括読み込みする。
    /// </summary>
    /// <param name="baselinePc">ベースラインとして使用するPCのエビデンス</param>
    void LoadFromPc(PcEvidence baselinePc);

    /// <summary>ベースラインをクリアする。</summary>
    void Clear();

    /// <summary>
    /// 対象PCの実行履歴をベースラインと突合する。
    /// </summary>
    /// <param name="pcName">対象PC名</param>
    /// <param name="actualHistory">対象PCの export_history エントリ一覧</param>
    /// <returns>突合レポート</returns>
    BaselineComparisonReport Compare(string pcName, IReadOnlyList<ExportHistoryEntry> actualHistory);

    /// <summary>
    /// 対象PCのエビデンスをベースラインと全カテゴリ一括照合する。
    /// 実行サマリ (ModuleName×Status) + SystemInfo + チェックリスト (OverallStatus + VerifyItems)。
    /// </summary>
    /// <param name="targetPc">照合対象のPCエビデンス</param>
    /// <returns>全カテゴリの照合結果を含むレポート</returns>
    BaselineComparisonReport CompareAll(PcEvidence targetPc);
}
