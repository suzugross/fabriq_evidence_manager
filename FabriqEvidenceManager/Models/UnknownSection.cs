namespace FabriqEvidenceManager.Models;

/// <summary>
/// 未知 manifest section に含まれるファイルの種別。
/// </summary>
public enum UnknownSectionFileType
{
    /// <summary>.csv 拡張子</summary>
    Csv,
    /// <summary>その他テキスト (.txt 等)</summary>
    Text,
}

/// <summary>
/// EvidenceConstants.KnownSections に無い manifest section の raw データ。
/// 将来 evidence_config に §23+ が追加された場合に、本アプリがクラッシュせず
/// 監査人が中身を確認できるよう Excel へ raw 出力するための保持型。
/// EVIDENCE_MANIFEST.md §4.1.2 「未知 section ID は raw 表示してクラッシュさせない」準拠。
/// 1 ファイル = 1 インスタンス (section が複数ファイルを持つ場合は複数生成)。
/// </summary>
public sealed class UnknownSection
{
    /// <summary>manifest 上のセクション ID</summary>
    public required string Id { get; init; }

    /// <summary>manifest 上のセクション title</summary>
    public required string Title { get; init; }

    /// <summary>raw ファイルの絶対パス</summary>
    public required string FilePath { get; init; }

    /// <summary>ファイル種別 (CSV / Text)</summary>
    public required UnknownSectionFileType FileType { get; init; }

    /// <summary>ファイル内容 (UTF-8 として読み込んだ全テキスト)</summary>
    public required string RawContent { get; init; }
}
