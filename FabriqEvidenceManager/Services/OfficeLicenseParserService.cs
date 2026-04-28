using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 22_OfficeLicense.txt と 22_OfficeVnextLicenses.csv をパースする。
/// evidence_config.ps1 §22 (v1.5.0+) の 4 ブロック構成:
///   1. ---- Office Click-to-Run Configuration (registry) ----
///        v1.5.0 以降は "DetectedAsSubscription:" 行も含む
///   2. ---- OSPP.vbs ----              (not-found 時のみ)
///      ---- OSPP.vbs path ----         (検出時のみ)
///      ---- cscript OSPP.vbs /dstatus (raw) ----   (検出時のみ)
///   3. ---- vNext Per-User License Files ----      (v1.5.0+、人間向けサマリ)
///   4. ---- INTERPRETATION ----                    (v1.5.0+、自動解釈)
/// vNext per-user の真実源は別ファイル 22_OfficeVnextLicenses.csv。
/// </summary>
public sealed class OfficeLicenseParserService : IOfficeLicenseParserService
{
    private const string OsppDstatusBlockSeparator = "---";
    private const string ConclusionPrefix = "CONCLUSION:";

    private enum BlockState
    {
        None,
        ClickToRun,
        OsppNotFound,
        OsppPath,
        OsppDstatus,
        VnextSummary,
        Interpretation,
    }

    public OfficeLicenseData Parse(string txtFilePath, string? vnextCsvPath = null)
    {
        var lines = File.ReadAllLines(txtFilePath, Encoding.UTF8);

        var c2rFields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? osppPath = null;
        var dstatusRaw = new StringBuilder();
        bool? osppShowsGrace = null;
        string? interpretationConclusion = null;
        int? provisionedCount = null;
        var state = BlockState.None;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimStart();

            if (trimmed.StartsWith("---- ", StringComparison.Ordinal))
            {
                // 順序重要: より具体的なヘッダから先に判定
                if (trimmed.Contains("OSPP.vbs path"))
                    state = BlockState.OsppPath;
                else if (trimmed.Contains("OSPP.vbs /dstatus"))
                    state = BlockState.OsppDstatus;
                else if (trimmed.Contains("OSPP.vbs"))
                    state = BlockState.OsppNotFound;
                else if (trimmed.Contains("Click-to-Run"))
                    state = BlockState.ClickToRun;
                else if (trimmed.Contains("vNext Per-User License Files"))
                    state = BlockState.VnextSummary;
                else if (trimmed.Contains("INTERPRETATION"))
                    state = BlockState.Interpretation;
                else
                    state = BlockState.None;
                continue;
            }

            switch (state)
            {
                case BlockState.ClickToRun:
                    if (string.IsNullOrWhiteSpace(rawLine)) break;
                    var kv = SplitKeyValue(rawLine);
                    if (kv is null) break;  // "(No Click-to-Run configuration ...)" 等
                    c2rFields[kv.Value.key] = kv.Value.value;
                    break;

                case BlockState.OsppPath:
                    if (osppPath is null && !string.IsNullOrWhiteSpace(rawLine))
                        osppPath = rawLine.Trim();
                    break;

                case BlockState.OsppDstatus:
                    dstatusRaw.AppendLine(rawLine);
                    break;

                case BlockState.Interpretation:
                    ApplyInterpretationLine(
                        rawLine,
                        ref osppShowsGrace,
                        ref provisionedCount,
                        ref interpretationConclusion);
                    break;

                // OsppNotFound: 内容は捨てる (osppPath は null のまま)
                // VnextSummary: 真実源は CSV のため raw 行は捨てる
                case BlockState.OsppNotFound:
                case BlockState.VnextSummary:
                case BlockState.None:
                default:
                    break;
            }
        }

        var clickToRun = c2rFields.ContainsKey("ProductReleaseIds")
            ? new OfficeClickToRunConfig
            {
                ProductReleaseIds = C2RField(c2rFields, "ProductReleaseIds"),
                VersionToReport = C2RField(c2rFields, "VersionToReport"),
                Platform = C2RField(c2rFields, "Platform"),
                CDNBaseUrl = C2RField(c2rFields, "CDNBaseUrl"),
                UpdateChannel = C2RField(c2rFields, "UpdateChannel"),
                AudienceData = C2RField(c2rFields, "AudienceData"),
                ClientCulture = C2RField(c2rFields, "ClientCulture"),
            }
            : null;

        // DetectedAsSubscription は ClickToRun 内の追加 key として落ちる。
        // 旧 evidence (v1.4 以前) には存在しないため、未検出時は null 維持。
        bool? isSubscriptionDetected = null;
        if (c2rFields.TryGetValue("DetectedAsSubscription", out var subVal)
            && bool.TryParse(subVal, out var subParsed))
        {
            isSubscriptionDetected = subParsed;
        }

        var dstatusText = dstatusRaw.ToString().TrimEnd('\r', '\n');
        var vnextEntries = LoadVnextCsv(vnextCsvPath);

        return new OfficeLicenseData
        {
            IsOfficeInstalled = clickToRun is not null || osppPath is not null,
            ClickToRun = clickToRun,
            OsppPath = osppPath,
            Products = ParseOsppProducts(dstatusText),
            OsppDstatusRaw = dstatusText,
            VnextEntries = vnextEntries,
            IsSubscriptionDetected = isSubscriptionDetected,
            OsppShowsGrace = osppShowsGrace,
            InterpretationConclusion = interpretationConclusion,
            ProvisionedCount = provisionedCount,
        };
    }

    /// <summary>
    /// INTERPRETATION ブロック内の 1 行から、フラグ系の value を抽出する。
    /// 想定行例:
    ///   "OSPP shows Grace/Notify:     True"
    ///   "vNext Provisioned:           2"
    ///   "  CONCLUSION: LICENSED (M365 subscription, 2 Provisioned vNext license(s))."
    /// 該当しない行 (NOTE / 空行 / その他) は黙って無視する。
    /// </summary>
    private static void ApplyInterpretationLine(
        string rawLine,
        ref bool? osppShowsGrace,
        ref int? provisionedCount,
        ref string? interpretationConclusion)
    {
        var trimmed = rawLine.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        // CONCLUSION 行は ":" を含むが key として「CONCLUSION」のみ拾うため先に処理
        var conclusionIdx = trimmed.IndexOf(ConclusionPrefix, StringComparison.Ordinal);
        if (conclusionIdx >= 0)
        {
            interpretationConclusion = trimmed[(conclusionIdx + ConclusionPrefix.Length)..].Trim();
            return;
        }

        var kv = SplitKeyValue(trimmed);
        if (kv is null) return;
        var (key, value) = kv.Value;

        if (key.Equals("OSPP shows Grace/Notify", StringComparison.Ordinal))
        {
            if (bool.TryParse(value, out var parsed)) osppShowsGrace = parsed;
        }
        else if (key.Equals("vNext Provisioned", StringComparison.Ordinal))
        {
            if (int.TryParse(value, out var parsed)) provisionedCount = parsed;
        }
        // "ProductReleaseIds (C2R):" / "Detected as subscription:" / "vNext licenses (any status):"
        // は二重ソース (ClickToRun と VnextEntries から導出可能) のため捨てる。
    }

    /// <summary>
    /// 22_OfficeVnextLicenses.csv をロードする。
    /// パス未指定 / ファイル不在 / ヘッダのみ の場合は空配列を返す
    /// (例外を投げない: 旧 evidence や CSV 出力失敗を許容)。
    /// </summary>
    private static IReadOnlyList<OfficeVnextLicenseEntry> LoadVnextCsv(string? csvPath)
    {
        if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
            return [];

        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return [];

        var headers = ParseCsvLine(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            headerIndex[headers[i]] = i;
        }

        var entries = new List<OfficeVnextLicenseEntry>(lines.Length - 1);
        for (var row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            var fields = ParseCsvLine(lines[row]);

            entries.Add(new OfficeVnextLicenseEntry
            {
                UserProfile      = CsvField(fields, headerIndex, "UserProfile"),
                Category         = CsvField(fields, headerIndex, "Category"),
                LicenseFile      = CsvField(fields, headerIndex, "LicenseFile"),
                LicenseType      = CsvField(fields, headerIndex, "LicenseType"),
                ProductReleaseId = CsvField(fields, headerIndex, "ProductReleaseId"),
                Status           = CsvField(fields, headerIndex, "Status"),
                IsTrial          = CsvField(fields, headerIndex, "IsTrial"),
                Beneficiary      = CsvField(fields, headerIndex, "Beneficiary"),
                LicenseId        = CsvField(fields, headerIndex, "LicenseId"),
                Acid             = CsvField(fields, headerIndex, "Acid"),
                TenantId         = CsvField(fields, headerIndex, "TenantId"),
                UserId           = CsvField(fields, headerIndex, "UserId"),
                HardwareIdBound  = CsvField(fields, headerIndex, "HardwareIdBound"),
                NotBefore        = CsvField(fields, headerIndex, "NotBefore"),
                NotAfter         = CsvField(fields, headerIndex, "NotAfter"),
                ParseStatus      = CsvField(fields, headerIndex, "ParseStatus"),
            });
        }

        return entries;
    }

    private static string CsvField(List<string> fields, Dictionary<string, int> headerIndex, string column)
    {
        if (!headerIndex.TryGetValue(column, out var idx)) return string.Empty;
        return idx < fields.Count ? fields[idx] : string.Empty;
    }

    /// <summary>
    /// ダブルクォート囲み + "" エスケープに対応した簡易 CSV 行パーサ。
    /// 既存の HostlistService / ChecklistParserService / EvidenceParserService と同流派。
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

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
        return fields;
    }

    private static string C2RField(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : string.Empty;

    private static (string key, string value)? SplitKeyValue(string line)
    {
        var idx = line.IndexOf(':');
        if (idx < 0) return null;
        return (line[..idx].Trim(), line[(idx + 1)..].Trim());
    }

    /// <summary>
    /// OSPP /dstatus raw text を製品ブロック配列にパース。
    /// 39 ハイフン separator + ---Processing--- / ---Exiting--- バナーで区切られた
    /// key:value 集合を 1 製品とみなす。LICENSE NAME と LICENSE STATUS のいずれかを
    /// 含むブロックのみ採用 (banner や空ブロックを除外)。
    /// </summary>
    private static IReadOnlyList<OfficeProductLicense> ParseOsppProducts(string raw)
    {
        var products = new List<OfficeProductLicense>();
        if (string.IsNullOrWhiteSpace(raw)) return products;

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        void Flush()
        {
            if (fields.ContainsKey("LICENSE NAME") || fields.ContainsKey("LICENSE STATUS"))
                products.Add(BuildProduct(fields));
            fields.Clear();
        }

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').TrimStart();

            if (line.StartsWith(OsppDstatusBlockSeparator, StringComparison.Ordinal))
            {
                Flush();
                continue;
            }
            if (string.IsNullOrWhiteSpace(line)) continue;

            var idx = line.IndexOf(':');
            if (idx < 0) continue;
            fields[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        Flush();

        return products;
    }

    private static OfficeProductLicense BuildProduct(Dictionary<string, string> f)
    {
        return new OfficeProductLicense
        {
            ProductId = f.GetValueOrDefault("PRODUCT ID", string.Empty),
            SkuId = f.GetValueOrDefault("SKU ID", string.Empty),
            LicenseName = f.GetValueOrDefault("LICENSE NAME", string.Empty),
            LicenseDescription = f.GetValueOrDefault("LICENSE DESCRIPTION", string.Empty),
            LicenseStatusText = ExtractStatusText(f.GetValueOrDefault("LICENSE STATUS", string.Empty)),
            ErrorCode = f.GetValueOrDefault("ERROR CODE", string.Empty),
            ErrorDescription = f.GetValueOrDefault("ERROR DESCRIPTION", string.Empty),
            RemainingGrace = f.GetValueOrDefault("REMAINING GRACE", string.Empty),
            LastFiveCharsProductKey = f.GetValueOrDefault("Last 5 characters of installed product key", string.Empty),
            KmsMachineName = f.GetValueOrDefault("KMS machine name from DNS", string.Empty),
        };
    }

    /// <summary>
    /// "---OOB_GRACE---" → "OOB_GRACE"。マーカーが無い値はそのまま返す。
    /// "---NOTIFICATIONS---  Notification Reason: ..." のような後続情報があっても
    /// status 部分のみを抽出する。
    /// </summary>
    private static string ExtractStatusText(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("---", StringComparison.Ordinal))
        {
            var parts = raw.Split("---", StringSplitOptions.None);
            if (parts.Length >= 2) return parts[1].Trim();
        }
        return raw;
    }
}
