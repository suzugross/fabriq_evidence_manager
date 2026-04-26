using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 22_OfficeLicense.txt をパースする。
/// evidence_config.ps1 §22 の 2〜4 ブロック構成:
///   1. ---- Office Click-to-Run Configuration (registry) ----
///   2. ---- OSPP.vbs ----              (not-found 時のみ)
///   3. ---- OSPP.vbs path ----         (検出時のみ)
///   4. ---- cscript OSPP.vbs /dstatus (raw) ----   (検出時のみ)
/// </summary>
public sealed class OfficeLicenseParserService : IOfficeLicenseParserService
{
    private const string OsppDstatusBlockSeparator = "---";

    private enum BlockState { None, ClickToRun, OsppNotFound, OsppPath, OsppDstatus }

    public OfficeLicenseData Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        var c2rFields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? osppPath = null;
        var dstatusRaw = new StringBuilder();
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

                // OsppNotFound: 内容は捨てる (osppPath は null のまま)
                case BlockState.OsppNotFound:
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

        var dstatusText = dstatusRaw.ToString().TrimEnd('\r', '\n');

        return new OfficeLicenseData
        {
            IsOfficeInstalled = clickToRun is not null || osppPath is not null,
            ClickToRun = clickToRun,
            OsppPath = osppPath,
            Products = ParseOsppProducts(dstatusText),
            OsppDstatusRaw = dstatusText,
        };
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
