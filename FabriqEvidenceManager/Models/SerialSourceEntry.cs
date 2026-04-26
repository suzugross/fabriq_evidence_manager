using System.Collections.Generic;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// シリアルナンバー取得試行 1 件の妥当性ステータス。
/// evidence_config.ps1 §10 の `[VALID, MATCH]` / `[INVALID: ...]` / `[QUERY FAILED: ...]` タグに対応。
/// </summary>
public enum SerialSourceValidity
{
    /// <summary>取得成功 (VALID)。さらに canonical と一致するかは MatchesCanonical で表す。</summary>
    Valid,
    /// <summary>取得はできたが妥当でない placeholder ("Default string" / "To Be Filled By O.E.M." 等)</summary>
    Invalid,
    /// <summary>WMI / レジストリのクエリ自体が失敗 (例外発生)</summary>
    QueryFailed,
}

/// <summary>
/// 10_SerialNumber.txt の "All Sources" ブロック 1 行に対応する取得試行レコード。
/// </summary>
public sealed class SerialSourceEntry
{
    /// <summary>
    /// 唯一の Primary canonical ソースラベル。これと一致するソースのみ
    /// 「お目当ての BIOS シリアル」とみなされ、それ以外はフォールバックとして警告対象。
    /// </summary>
    public const string PrimaryCanonicalLabel = "Win32_BIOS.SerialNumber";

    /// <summary>例: "Win32_BIOS.SerialNumber"</summary>
    public required string Label { get; init; }

    /// <summary>raw 値 (空文字列可)</summary>
    public required string Value { get; init; }

    public required SerialSourceValidity Validity { get; init; }

    /// <summary>無効/失敗時の理由 (例: "To be filled by O.E.M." / "empty" / 例外メッセージ抜粋)。Valid 時は null。</summary>
    public string? ValidityReason { get; init; }

    /// <summary>Valid かつ canonical 値と一致 (`[VALID, MATCH]`)。</summary>
    public bool MatchesCanonical { get; init; }

    /// <summary>
    /// 採用候補ソースか。BaseBoard.SerialNumber は record-only (motherboard SN, not PC SN)
    /// で false。ハードコード判定 (Label 名から決定)。
    /// </summary>
    public bool IsCanonicalCandidate { get; init; }

    /// <summary>「採用された」1 行 (Canonical Serial Number ブロックの Source と一致)</summary>
    public bool IsSelectedCanonical { get; init; }

    /// <summary>
    /// Label の短縮表記。狭幅 UI の併記表示や警告メッセージで使用。
    /// </summary>
    public static string ShortLabel(string? fullLabel) => fullLabel switch
    {
        "Win32_BIOS.SerialNumber" => "BIOS",
        "Win32_ComputerSystemProduct.IdentifyingNumber" => "CSProduct",
        "Win32_SystemEnclosure.SerialNumber" => "Enclosure",
        "Win32_BaseBoard.SerialNumber" => "BaseBoard",
        "Registry SystemSerialNumber" => "Registry",
        null or "" => "?",
        _ => fullLabel,
    };
}

/// <summary>
/// 10_SerialNumber.txt 全体のパース結果。
/// </summary>
public sealed class SerialNumberDetail
{
    /// <summary>採用された SN 値。Unretrievable 時は空文字列。</summary>
    public required string CanonicalValue { get; init; }

    /// <summary>
    /// 採用ソースのラベル (例: "Win32_BIOS.SerialNumber")。
    /// Unretrievable 時は空文字列。
    /// </summary>
    public required string CanonicalSourceLabel { get; init; }

    /// <summary>全ソース試行結果 (採用された 1 件以外も含む全行)</summary>
    public required IReadOnlyList<SerialSourceEntry> Sources { get; init; }

    /// <summary>Win32_ComputerSystemProduct.UUID (audit reference, SN ではない)</summary>
    public string ReferenceUuid { get; init; } = string.Empty;

    /// <summary>
    /// 採用ソースが Primary canonical (= Win32_BIOS.SerialNumber) かどうか。
    /// false なら「フォールバック取得」として UI/Excel で警告対象。
    /// </summary>
    public bool IsFromPrimarySource =>
        CanonicalSourceLabel == SerialSourceEntry.PrimaryCanonicalLabel;
}
