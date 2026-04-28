using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 26_BatteryReport.html を正規表現でパースする。
/// powercfg /batteryreport が生成する Microsoft 純正書式の HTML は
/// <c>&lt;span class="label"&gt;KEY&lt;/span&gt;&lt;/td&gt;&lt;td&gt;VALUE</c> という
/// label-value パターンが安定している (Win10〜Win11 で互換)。
///
/// "Installed batteries" セクション最初のバッテリエントリのみを採用する
/// (複数搭載 PC は稀、最初の 1 つで実用上十分)。HtmlAgilityPack のような
/// HTML 専用パーサ依存は導入せず、シンプルさを優先。
/// </summary>
public sealed partial class BatteryReportParserService : IBatteryReportParserService
{
    [GeneratedRegex(@"<span class=""label"">([^<]+)</span></td><td>([^<]*)", RegexOptions.IgnoreCase)]
    private static partial Regex LabelValuePattern();

    /// <summary>"42,068 mWh" / "42068 mWh" / "42,068" → 42068 を抽出。</summary>
    [GeneratedRegex(@"([\d,]+)")]
    private static partial Regex NumericPattern();

    public BatteryReportData Parse(string filePath)
    {
        var html = File.ReadAllText(filePath);

        // label → 最初に出現した value を採用 (BATTERIES の Installed batteries が先頭で出る)
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in LabelValuePattern().Matches(html))
        {
            var key = m.Groups[1].Value.Trim();
            if (fields.ContainsKey(key)) continue;
            var rawValue = m.Groups[2].Value.Trim();
            fields[key] = rawValue;
        }

        return new BatteryReportData
        {
            Name         = fields.GetValueOrDefault("NAME", string.Empty),
            Manufacturer = fields.GetValueOrDefault("MANUFACTURER", string.Empty),
            SerialNumber = fields.GetValueOrDefault("SERIAL NUMBER", string.Empty),
            Chemistry    = fields.GetValueOrDefault("CHEMISTRY", string.Empty),
            DesignCapacityMwh     = ExtractMwh(fields.GetValueOrDefault("DESIGN CAPACITY", string.Empty)),
            FullChargeCapacityMwh = ExtractMwh(fields.GetValueOrDefault("FULL CHARGE CAPACITY", string.Empty)),
            CycleCount = ParseIntStripCommas(fields.GetValueOrDefault("CYCLE COUNT", string.Empty)),
        };
    }

    /// <summary>"42,068 mWh" / "42068 mWh" → 42068。失敗時 null。</summary>
    private static int? ExtractMwh(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var m = NumericPattern().Match(value);
        return m.Success && int.TryParse(m.Groups[1].Value.Replace(",", string.Empty),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
    }

    private static int? ParseIntStripCommas(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value.Replace(",", string.Empty),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
    }
}
