using System;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 管理者が PC ごとに残せる自由記述メモ。
/// JSON 形式で <c>{PC ルートディレクトリ}/manager_memo.json</c> に永続化される。
/// 現状は 1 PC = 1 メモ (履歴・複数メモは将来課題)。
/// </summary>
public sealed class PcMemo
{
    /// <summary>メモ本文 (multi-line 可)</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>最終更新日時</summary>
    public DateTimeOffset LastUpdatedAt { get; init; }

    /// <summary>最終更新者 (Environment.UserName ベース)</summary>
    public string LastUpdatedBy { get; init; } = string.Empty;
}
