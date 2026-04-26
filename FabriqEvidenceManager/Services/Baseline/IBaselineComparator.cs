using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Baseline;

/// <summary>
/// ベースライン突合の単一カテゴリを担う抽象。
/// 各実装は自身のキャッシュ + 比較ロジックを内包し、<see cref="BaselineService"/> から
/// オーケストレートされて <see cref="BaselineComparisonReport"/> の該当フィールドを書き込む。
///
/// 新カテゴリ (例: WindowsUpdate / FW ルール / OptionalFeatures 等) の追加は
/// 本 interface 実装 + DI 登録のみで完結する設計。
/// 互換性: report への書き込み先は実装が知っている (タイプセーフなフィールド単位)。
/// </summary>
public interface IBaselineComparator
{
    /// <summary>カテゴリ識別子。Phase 2 で UI チェックボックスのキーに使う安定 ID。</summary>
    string CategoryId { get; }

    /// <summary>UI 表示名 (日本語)。</summary>
    string DisplayName { get; }

    /// <summary>デフォルトで有効なカテゴリかどうか (Phase 2 でユーザがオフ可)。</summary>
    bool IsEnabledByDefault { get; }

    /// <summary>ベースラインデータが既にキャッシュされているか。</summary>
    bool HasBaselineData { get; }

    /// <summary>
    /// 指定 PC からこのカテゴリのベースラインデータを取り込む。
    /// PC に該当データがない場合の挙動は実装依存 (前回キャッシュ保持 or null 化)。
    /// </summary>
    void CacheBaseline(PcEvidence baselinePc);

    /// <summary>キャッシュ済みベースラインを破棄する。</summary>
    void ClearBaseline();

    /// <summary>
    /// 対象 PC とベースラインを比較し、結果を <paramref name="report"/> の該当フィールドに書き込む。
    /// HasBaselineData=false または対象 PC にデータがない場合は何もしない (silent skip)。
    /// </summary>
    void Compare(BaselineComparisonReport report, PcEvidence targetPc);
}
