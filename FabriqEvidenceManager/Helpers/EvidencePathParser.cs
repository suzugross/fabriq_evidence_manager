using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace FabriqEvidenceManager.Helpers;

/// <summary>
/// evidence ディレクトリ名から PC名 と シリアル(HardwareUniqueId) を抽出する。
///
/// log_uploader 親フォルダ名:
///   {yyyy_MM_dd_HHmmss}_{PCName}_{SerialNumber}_evidence
///   → 先頭タイムスタンプと末尾 _evidence をアンカーに、最後の "_" で PCName と SerialNumber を分離
///   → PCName に "_" が含まれる可能性があるため右端分割
/// </summary>
public static partial class EvidencePathParser
{
    /// <summary>新形式親フォルダの固定サフィックス</summary>
    private const string NewFormatSuffix = "_evidence";

    // 日付+時刻パターン: yyyy_MM_dd_HHmmss (例: 2026_02_26_132839)
    [GeneratedRegex(@"^\d{4}_\d{2}_\d{2}_\d{6}_")]
    private static partial Regex DateTimePrefix();

    /// <summary>
    /// フォルダ名が <c>_evidence</c> サフィックス付きかどうかを判定する。
    /// </summary>
    public static bool IsNewFormatFolder(string folderName)
        => folderName.EndsWith(NewFormatSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// log_uploader 親フォルダ名から PC名・シリアル・収集日を抽出する。
    /// <para>
    /// 命名規則: <c>{yyyy_MM_dd_HHmmss}_{PCName}_{SerialNumber}_evidence</c>
    /// </para>
    /// <para>
    /// PCName に <c>_</c> が含まれる可能性があるため、中間部分の<b>最後の <c>_</c></b> で
    /// PCName と SerialNumber を分離する。SerialNumber は BIOS SN / MAC / UNKNOWN であり
    /// <c>_</c> を含まない前提。
    /// </para>
    /// </summary>
    /// <param name="folderName">親フォルダ名 (例: "2026_03_12_143025_NEW-PC-01_ABC123XYZ_evidence")</param>
    /// <param name="pcName">抽出されたPC名</param>
    /// <param name="serial">抽出されたシリアル</param>
    /// <param name="collectionDate">抽出された収集日 (yyyy_MM_dd 形式)</param>
    public static bool TryParseNewFormat(
        string folderName,
        [NotNullWhen(true)] out string? pcName,
        [NotNullWhen(true)] out string? serial,
        [NotNullWhen(true)] out string? collectionDate)
    {
        pcName = null;
        serial = null;
        collectionDate = null;

        if (string.IsNullOrWhiteSpace(folderName))
            return false;

        // _evidence サフィックスを確認・除去
        if (!folderName.EndsWith(NewFormatSuffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var withoutSuffix = folderName[..^NewFormatSuffix.Length];

        // タイムスタンプ部分を確認・除去 (yyyy_MM_dd_HHmmss_ = 18文字)
        var dtMatch = DateTimePrefix().Match(withoutSuffix);
        if (!dtMatch.Success)
            return false;

        collectionDate = withoutSuffix[..17]; // yyyy_MM_dd_HHmmss (フォルダ単位で一意にする)
        var middle = withoutSuffix[dtMatch.Length..]; // "{PCName}_{SerialNumber}"

        if (string.IsNullOrEmpty(middle))
            return false;

        // PCName に "_" が含まれる可能性があるため、最後の "_" で分割
        var lastSep = middle.LastIndexOf('_');
        if (lastSep <= 0 || lastSep >= middle.Length - 1)
            return false;

        pcName = middle[..lastSep];
        serial = middle[(lastSep + 1)..];

        return !string.IsNullOrEmpty(pcName) && !string.IsNullOrEmpty(serial);
    }
}
