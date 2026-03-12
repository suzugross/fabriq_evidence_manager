using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace FabriqEvidenceManager.Helpers;

/// <summary>
/// evidence ディレクトリ名・ファイル名から PC名 と シリアル(HardwareUniqueId) を抽出する。
///
/// 旧形式 (カテゴリ内サブフォルダ名 / ファイル名):
///   日付のみ  : {yyyy_MM_dd}_{serial}_{pcName}         (auto_capture, bitlocker)
///   日付+時刻 : {yyyy_MM_dd_HHmmss}_{serial}_{pcName}  (pc_information, checklist, export_history)
///   → 最初の "_" で serial と pcName を分離 (どちらも "_" を含まない前提)
///
/// 新形式 (log_uploader 親フォルダ名):
///   {yyyy_MM_dd_HHmmss}_{PCName}_{SerialNumber}_evidence
///   → 先頭タイムスタンプと末尾 _evidence をアンカーに、最後の "_" で PCName と SerialNumber を分離
///   → PCName に "_" が含まれる可能性があるため右端分割
/// </summary>
public static partial class EvidencePathParser
{
    /// <summary>新形式親フォルダの固定サフィックス</summary>
    private const string NewFormatSuffix = "_evidence";
    // 日付+時刻パターン: yyyy_MM_dd_HHmmss (例: 2026_02_26_132839)
    // 日付のみパターン:  yyyy_MM_dd          (例: 2026_02_26)
    // 長い方を先にマッチさせることで誤判定を防ぐ
    [GeneratedRegex(@"^\d{4}_\d{2}_\d{2}_\d{6}_")]
    private static partial Regex DateTimePrefix();

    [GeneratedRegex(@"^\d{4}_\d{2}_\d{2}_")]
    private static partial Regex DateOnlyPrefix();

    /// <summary>
    /// ディレクトリ名またはファイル名（拡張子除去済み）から PC名・シリアル・収集日 を抽出する。
    /// </summary>
    /// <param name="name">ディレクトリ名 or ファイル名ステム (例: "2026_02_26_T2NXCV06Y22208C_NEW-PC-01")</param>
    /// <param name="pcName">抽出されたPC名</param>
    /// <param name="serial">抽出されたシリアル(HardwareUniqueId)</param>
    /// <param name="collectionDate">抽出された収集日 (yyyy_MM_dd 形式)</param>
    /// <returns>抽出に成功した場合 true</returns>
    public static bool TryParse(
        string name,
        [NotNullWhen(true)] out string? pcName,
        [NotNullWhen(true)] out string? serial,
        [NotNullWhen(true)] out string? collectionDate)
    {
        pcName = null;
        serial = null;
        collectionDate = null;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        // 日付プレフィックスを除去 (長い方を先に試す)
        // どちらの場合も日付部分は先頭 10 文字 (yyyy_MM_dd)
        string remainder;
        var dtMatch = DateTimePrefix().Match(name);
        if (dtMatch.Success)
        {
            collectionDate = name[..10]; // yyyy_MM_dd
            remainder = name[dtMatch.Length..];
        }
        else
        {
            var dMatch = DateOnlyPrefix().Match(name);
            if (!dMatch.Success)
                return false;
            collectionDate = name[..10]; // yyyy_MM_dd
            remainder = name[dMatch.Length..];
        }

        // remainder = "{serial}_{pcName}"
        // 最初の "_" でシリアルとPC名を分離
        var separatorIndex = remainder.IndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= remainder.Length - 1)
            return false;

        serial = remainder[..separatorIndex];
        pcName = remainder[(separatorIndex + 1)..];

        return !string.IsNullOrEmpty(serial) && !string.IsNullOrEmpty(pcName);
    }

    /// <summary>
    /// チェックリストやエクスポート履歴のファイル名からPC名・シリアル・収集日を抽出する。
    /// プレフィックス ("checklist_", "history_export_") を除去してから TryParse に委譲する。
    /// </summary>
    /// <param name="fileNameWithoutExtension">拡張子なしファイル名</param>
    /// <param name="prefix">除去すべきプレフィックス (例: "checklist_")</param>
    /// <param name="pcName">抽出されたPC名</param>
    /// <param name="serial">抽出されたシリアル</param>
    /// <param name="collectionDate">抽出された収集日 (yyyy_MM_dd 形式)</param>
    public static bool TryParseWithPrefix(
        string fileNameWithoutExtension,
        string prefix,
        [NotNullWhen(true)] out string? pcName,
        [NotNullWhen(true)] out string? serial,
        [NotNullWhen(true)] out string? collectionDate)
    {
        pcName = null;
        serial = null;
        collectionDate = null;

        if (!fileNameWithoutExtension.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var body = fileNameWithoutExtension[prefix.Length..];
        return TryParse(body, out pcName, out serial, out collectionDate);
    }

    /// <summary>
    /// フォルダ名が新形式 (<c>_evidence</c> サフィックス付き) かどうかを判定する。
    /// </summary>
    public static bool IsNewFormatFolder(string folderName)
        => folderName.EndsWith(NewFormatSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 新形式の log_uploader 親フォルダ名から PC名・シリアル・収集日を抽出する。
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
