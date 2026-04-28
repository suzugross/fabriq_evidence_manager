using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 25_Certificates.csv をパースする。
/// evidence_config.ps1 §25 (v1.4.0+) が Export-Csv -Encoding UTF8 で出力する
/// 10 列 CSV を読み込み、4 ストア統合の CertificateEntry リストにする。
/// CSV パーサ流派は OfficeLicenseParserService / HostlistService と同じ
/// (ダブルクォート + "" エスケープ対応の private static).
/// </summary>
public sealed class CertificatesParserService : ICertificatesParserService
{
    /// <summary>
    /// PowerShell Export-Csv の DateTime 既定書式は cultural-invariant な
    /// "yyyy/MM/dd H:mm:ss" 系。実フィクスチャは "2013/08/01 21:00:00" のような
    /// 24h スラッシュ区切り。明示書式 + InvariantCulture で受ける。
    /// </summary>
    private static readonly string[] CsvDateTimeFormats =
    {
        "yyyy/MM/dd H:mm:ss",
        "yyyy/M/d H:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
    };

    public IReadOnlyList<CertificateEntry> Parse(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length < 2) return [];

        var headers = ParseCsvLine(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
            headerIndex[headers[i]] = i;

        var entries = new List<CertificateEntry>(lines.Length - 1);
        for (var row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            var fields = ParseCsvLine(lines[row]);

            entries.Add(new CertificateEntry
            {
                Store                = CsvField(fields, headerIndex, "Store"),
                Subject              = CsvField(fields, headerIndex, "Subject"),
                Issuer               = CsvField(fields, headerIndex, "Issuer"),
                Thumbprint           = CsvField(fields, headerIndex, "Thumbprint"),
                NotBefore            = ParseCsvDateTime(CsvField(fields, headerIndex, "NotBefore")),
                NotAfter             = ParseCsvDateTime(CsvField(fields, headerIndex, "NotAfter")),
                HasPrivateKey        = ParseBool(CsvField(fields, headerIndex, "HasPrivateKey")),
                EnhancedKeyUsageList = CsvField(fields, headerIndex, "EnhancedKeyUsageList"),
                FriendlyName         = CsvField(fields, headerIndex, "FriendlyName"),
                SerialNumber         = CsvField(fields, headerIndex, "SerialNumber"),
            });
        }
        return entries;
    }

    private static string CsvField(List<string> fields, Dictionary<string, int> headerIndex, string column)
    {
        if (!headerIndex.TryGetValue(column, out var idx)) return string.Empty;
        return idx < fields.Count ? fields[idx] : string.Empty;
    }

    private static DateTime? ParseCsvDateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParseExact(value, CsvDateTimeFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return parsed;
        // ロケール / 代替書式 fallback
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return parsed;
        return null;
    }

    private static bool ParseBool(string value)
        => bool.TryParse(value, out var parsed) && parsed;

    /// <summary>
    /// ダブルクォート + "" エスケープ対応の簡易 CSV 行パーサ。
    /// 既存の OfficeLicenseParserService / HostlistService と同流派。
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
}
