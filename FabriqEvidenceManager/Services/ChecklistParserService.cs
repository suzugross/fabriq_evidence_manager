using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// チェックリスト HTML / 実行履歴 CSV のパーサー。
/// HTML は正規表現ベースで必要な表データのみ抽出する (外部ライブラリ不要)。
/// </summary>
public sealed partial class ChecklistParserService : IChecklistParserService
{
    // ---- Regex (ソースジェネレータ) ----

    /// <summary>全体判定バッジ: overall-ok / overall-ng / overall-partial / overall-ca</summary>
    [GeneratedRegex(@"class=""overall-badge\s+overall-(\w+)"">(\w+)</div>")]
    private static partial Regex OverallBadgePattern();

    /// <summary>verify-table の &lt;tr&gt; 行: &lt;td&gt;4列</summary>
    [GeneratedRegex(@"<tr[^>]*>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>.*?<span[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex VerifyRowPattern();

    /// <summary>モジュールテーブルの &lt;tr&gt; 行: 6列 (#, Module, Category, Result badge, Time, Message)</summary>
    [GeneratedRegex(@"<tr[^>]*>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>.*?<span[^>]*>(.*?)</span>.*?</td>\s*<td[^>]*>(.*?)</td>\s*<td[^>]*>(.*?)</td>", RegexOptions.Singleline)]
    private static partial Regex ModuleRowPattern();

    /// <summary>HTMLタグ除去</summary>
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    /// <summary>verify-table セクション抽出</summary>
    [GeneratedRegex(@"<table\s+class=""verify-table"">(.*?)</table>", RegexOptions.Singleline)]
    private static partial Regex VerifyTablePattern();

    /// <summary>メインテーブル (class="table-wrap" 内) 抽出</summary>
    [GeneratedRegex(@"<div\s+class=""table-wrap"">(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex MainTablePattern();

    // ================================================================
    // チェックリスト HTML パース
    // ================================================================

    /// <inheritdoc />
    public ChecklistResult ParseChecklistHtml(string filePath)
    {
        var html = File.ReadAllText(filePath, Encoding.UTF8);

        // 全体判定
        var overallMatch = OverallBadgePattern().Match(html);
        var overallStatus = overallMatch.Success ? overallMatch.Groups[2].Value : "";

        // verify-table セクション
        var verifyItems = new List<ChecklistVerifyItem>();
        var verifyTableMatch = VerifyTablePattern().Match(html);
        if (verifyTableMatch.Success)
        {
            var tableHtml = verifyTableMatch.Groups[1].Value;
            foreach (Match row in VerifyRowPattern().Matches(tableHtml))
            {
                verifyItems.Add(new ChecklistVerifyItem
                {
                    ItemName = StripHtml(row.Groups[1].Value),
                    Expected = StripHtml(row.Groups[2].Value),
                    Actual = StripHtml(row.Groups[3].Value),
                    Status = StripHtml(row.Groups[4].Value),
                });
            }
        }

        // モジュール実行テーブル
        var moduleItems = new List<ChecklistModuleItem>();
        var mainTableMatch = MainTablePattern().Match(html);
        if (mainTableMatch.Success)
        {
            var tableHtml = mainTableMatch.Groups[1].Value;
            foreach (Match row in ModuleRowPattern().Matches(tableHtml))
            {
                moduleItems.Add(new ChecklistModuleItem
                {
                    Order = StripHtml(row.Groups[1].Value),
                    ModuleName = StripHtml(row.Groups[2].Value),
                    Category = StripHtml(row.Groups[3].Value),
                    Status = StripHtml(row.Groups[4].Value),
                    Time = StripHtml(row.Groups[5].Value),
                    Message = StripHtml(row.Groups[6].Value),
                });
            }
        }

        return new ChecklistResult
        {
            OverallStatus = overallStatus,
            VerifyItems = verifyItems,
            ModuleItems = moduleItems,
        };
    }

    /// <summary>HTMLタグを除去してトリムする。</summary>
    private static string StripHtml(string html) =>
        HtmlTagPattern().Replace(html, "").Trim();

    // ================================================================
    // 実行履歴 CSV パース
    // ================================================================

    /// <inheritdoc />
    public IReadOnlyList<ExportHistoryEntry> ParseExportHistory(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length < 2) return [];

        var headers = ParseCsvLine(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
            headerIndex[headers[i].Trim()] = i;

        var results = new List<ExportHistoryEntry>();
        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;

            var fields = ParseCsvLine(lines[row]);
            results.Add(new ExportHistoryEntry
            {
                Timestamp = F("Timestamp"),
                KanriNo = F("KanriNo"),
                PCName = F("PCName"),
                ModuleName = F("ModuleName"),
                Category = F("Category"),
                Status = F("Status"),
                Message = F("Message"),
                WindowsUser = F("WindowsUser"),
                Worker = F("Worker"),
                MediaSerial = F("MediaSerial"),
                SessionID = F("SessionID"),
            });

            string F(string col) =>
                headerIndex.TryGetValue(col, out var idx) && idx < fields.Length
                    ? fields[idx].Trim()
                    : string.Empty;
        }

        return results;
    }

    /// <summary>
    /// 簡易CSVパーサー。ダブルクォート囲みに対応。
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
