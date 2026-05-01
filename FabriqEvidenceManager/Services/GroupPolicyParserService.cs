using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 24_GroupPolicySummary.txt をパースする。
/// evidence_config v1.6.0+ §24 (kernel 3.0.0+) の TXT サマリは Key:Value 形式の
/// 5 〜 6 行構成 (gpresult exit code / HTML file size / PartOfDomain / Domain / ExecutingUser)。
///
/// HTML 本体 (24_GroupPolicy.html) は読み込まずファイルパスのみ Model に格納する設計。
/// 240KB 規模の HTML を string として保持するメモリ消費を回避する。
/// </summary>
public sealed class GroupPolicyParserService : IGroupPolicyParserService
{
    public GroupPolicyReport Parse(string summaryFilePath, string? htmlFilePath)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(summaryFilePath, Encoding.UTF8))
        {
            var trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // ヘッダ行 ("====" / "Group Policy Report" / "Running gpresult..." / "(NOTE: ...)") をスキップ
            if (trimmed.StartsWith("====", StringComparison.Ordinal)
                || trimmed.StartsWith("Group Policy Report", StringComparison.Ordinal)
                || trimmed.StartsWith("Running ", StringComparison.Ordinal)
                || trimmed.StartsWith("Temp HTML", StringComparison.Ordinal)
                || trimmed.StartsWith("Target HTML", StringComparison.Ordinal)
                || trimmed.StartsWith("(", StringComparison.Ordinal))
                continue;

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = trimmed[..colonIdx].Trim();
            var value = trimmed[(colonIdx + 1)..].Trim();
            fields[key] = value;
        }

        return new GroupPolicyReport
        {
            ExecutionStatus   = ParseIntNullable(fields, "gpresult exit code"),
            HtmlFileSizeBytes = ParseHtmlSize(fields),
            PartOfDomain      = ParseBoolNullable(fields, "PartOfDomain"),
            Domain            = fields.GetValueOrDefault("Domain", string.Empty),
            ExecutingUser     = fields.GetValueOrDefault("ExecutingUser", string.Empty),
            HtmlFilePath      = htmlFilePath,
        };
    }

    /// <summary>"HTML file size: 240972 bytes" → 240972 を抽出。"bytes" 接尾辞を許容。</summary>
    private static long? ParseHtmlSize(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("HTML file size", out var raw)) return null;

        // " bytes" を取り除いて数値部分のみ残す
        var spaceIdx = raw.IndexOf(' ');
        var numeric = spaceIdx > 0 ? raw[..spaceIdx] : raw;
        return long.TryParse(numeric, out var parsed) ? parsed : null;
    }

    private static int? ParseIntNullable(Dictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var v) && int.TryParse(v, out var parsed) ? parsed : null;

    private static bool? ParseBoolNullable(Dictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var v) && bool.TryParse(v, out var parsed) ? parsed : null;
}
