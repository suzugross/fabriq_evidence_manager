using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// セクション処理結果のステータス。EVIDENCE_MANIFEST.md §3.3。
/// </summary>
public enum SectionStatus
{
    /// <summary>セクション完了、files[] すべて書き込まれた</summary>
    Success,
    /// <summary>意図的スキップ (Server-only セクションを Client で実行 等)</summary>
    Skipped,
    /// <summary>例外発生でセクション完了不可。files[] は常に空配列</summary>
    Failed,
    /// <summary>単一セクション内の複数処理の一部のみ成功</summary>
    Partial,
}

/// <summary>
/// manifest sections[] の 1 エントリ。EVIDENCE_MANIFEST.md §3.2。
/// </summary>
public sealed class ManifestSection
{
    /// <summary>セクション ID。"01"〜"22" または副 ID ("8b" 等)</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>セクション名 (例: "System Basic Info")</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// manifest 親ディレクトリからの相対パス配列。
    /// "/" で終わる要素はディレクトリを表す (EVIDENCE_MANIFEST.md §6)。
    /// </summary>
    [JsonPropertyName("files")]
    public required IReadOnlyList<string> Files { get; init; }

    [JsonPropertyName("status")]
    public required SectionStatus Status { get; init; }

    private readonly string? _reason;
    /// <summary>
    /// 非 Success 時の理由文字列。
    /// JSON 上は Success 時 null or "" 両方が来うるため、init 時に null 正規化する。
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason
    {
        get => _reason;
        init => _reason = string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>セクション処理経過時間 (ミリ秒)</summary>
    [JsonPropertyName("elapsedMs")]
    public int ElapsedMs { get; init; }

    /// <summary>files[] のうち通常ファイル (`/` で終わらないもの)</summary>
    [JsonIgnore]
    public IEnumerable<string> RegularFiles => Files.Where(f => !f.EndsWith('/'));

    /// <summary>files[] のうちディレクトリ (`/` で終わるもの)</summary>
    [JsonIgnore]
    public IEnumerable<string> Directories => Files.Where(f => f.EndsWith('/'));
}
