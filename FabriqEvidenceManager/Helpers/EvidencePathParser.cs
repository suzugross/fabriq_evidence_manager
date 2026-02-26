using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace FabriqEvidenceManager.Helpers;

/// <summary>
/// evidence ディレクトリ名・ファイル名から PC名 と シリアル(HardwareUniqueId) を抽出する。
///
/// fabriq の命名規則:
///   日付のみ  : {yyyy_MM_dd}_{serial}_{pcName}         (auto_capture, bitlocker)
///   日付+時刻 : {yyyy_MM_dd_HHmmss}_{serial}_{pcName}  (pc_information, checklist, export_history)
///
/// PC名・シリアルともにアンダースコアは含まない
/// (Windows NetBIOS名は英数字+ハイフンのみ / シリアルも同様) ため、
/// 日付プレフィックスを除去した後の最初の "_" で serial と pcName を分離できる。
/// </summary>
public static partial class EvidencePathParser
{
    // 日付+時刻パターン: yyyy_MM_dd_HHmmss (例: 2026_02_26_132839)
    // 日付のみパターン:  yyyy_MM_dd          (例: 2026_02_26)
    // 長い方を先にマッチさせることで誤判定を防ぐ
    [GeneratedRegex(@"^\d{4}_\d{2}_\d{2}_\d{6}_")]
    private static partial Regex DateTimePrefix();

    [GeneratedRegex(@"^\d{4}_\d{2}_\d{2}_")]
    private static partial Regex DateOnlyPrefix();

    /// <summary>
    /// ディレクトリ名またはファイル名（拡張子除去済み）から PC名 と シリアル を抽出する。
    /// </summary>
    /// <param name="name">ディレクトリ名 or ファイル名ステム (例: "2026_02_26_T2NXCV06Y22208C_NEW-PC-01")</param>
    /// <param name="pcName">抽出されたPC名</param>
    /// <param name="serial">抽出されたシリアル(HardwareUniqueId)</param>
    /// <returns>抽出に成功した場合 true</returns>
    public static bool TryParse(string name, [NotNullWhen(true)] out string? pcName, [NotNullWhen(true)] out string? serial)
    {
        pcName = null;
        serial = null;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        // 日付プレフィックスを除去 (長い方を先に試す)
        string remainder;
        var dtMatch = DateTimePrefix().Match(name);
        if (dtMatch.Success)
        {
            remainder = name[dtMatch.Length..];
        }
        else
        {
            var dMatch = DateOnlyPrefix().Match(name);
            if (!dMatch.Success)
                return false;
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
    /// チェックリストやエクスポート履歴のファイル名からPC名とシリアルを抽出する。
    /// プレフィックス ("checklist_", "history_export_") を除去してから TryParse に委譲する。
    /// </summary>
    /// <param name="fileNameWithoutExtension">拡張子なしファイル名</param>
    /// <param name="prefix">除去すべきプレフィックス (例: "checklist_")</param>
    /// <param name="pcName">抽出されたPC名</param>
    /// <param name="serial">抽出されたシリアル</param>
    public static bool TryParseWithPrefix(
        string fileNameWithoutExtension,
        string prefix,
        [NotNullWhen(true)] out string? pcName,
        [NotNullWhen(true)] out string? serial)
    {
        pcName = null;
        serial = null;

        if (!fileNameWithoutExtension.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var body = fileNameWithoutExtension[prefix.Length..];
        return TryParse(body, out pcName, out serial);
    }
}
