namespace FabriqEvidenceManager.Models;

/// <summary>
/// 24_GroupPolicySummary.txt のパース結果と 24_GroupPolicy.html へのファイルパスを集約。
/// evidence_config v1.6.0+ §24 (kernel 3.0.0+) は gpresult /h の HTML 出力 (240KB 規模)
/// と、それを補足する Key:Value 形式の TXT サマリの 2 ファイルを出力する。
///
/// 設計方針:
///  - HTML は読み込まず HtmlFilePath に絶対パスのみ保持 (メモリ消費抑制)
///  - 実行時は Process.Start で OS 既定ブラウザ表示 (PcDetailWindow のボタン経由)
///  - Excel 出力時は構造化フィールドのみ書き出し、HTML hyperlink は入れない
///    (delivery folder 相対パス計算が複雑なため、後続フェーズに保留)
/// </summary>
public sealed class GroupPolicyReport
{
    /// <summary>gpresult exit code。0 = 成功。null は TXT に該当行が無かった場合。</summary>
    public int? ExecutionStatus { get; init; }

    /// <summary>HTML ファイルサイズ (bytes)。TXT サマリに記録された値。</summary>
    public long? HtmlFileSizeBytes { get; init; }

    /// <summary>ドメイン参加状態 (§05 DomainStatus と整合チェック可能)。null は不明。</summary>
    public bool? PartOfDomain { get; init; }

    /// <summary>ドメイン名 (例: "e-cri.local"、WORKGROUP 機では空)。</summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>gpresult を実行したユーザ (= キッティング作業者)。end-user とは限らない。</summary>
    public string ExecutingUser { get; init; } = string.Empty;

    /// <summary>HTML ファイルの絶対パス。null は HTML 不在 (gpresult 失敗時など)。</summary>
    public string? HtmlFilePath { get; init; }

    /// <summary>gpresult が成功したか (ExecutionStatus == 0)。Excel/UI の status 表示で使用。</summary>
    public bool IsSuccess => ExecutionStatus == 0;
}
