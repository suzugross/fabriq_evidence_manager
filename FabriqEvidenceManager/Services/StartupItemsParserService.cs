using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 28_StartupItems.csv をパースする。
/// evidence_config v1.6.0+ §28 (kernel 3.0.0+) が出力する 6 列 CSV を
/// header-index lookup で読み込む (列順依存しない)。
/// CSV パーサ流派は CertificatesParserService / EnvironmentVariablesParserService と同じ。
/// </summary>
public sealed class StartupItemsParserService : IStartupItemsParserService
{
    public IReadOnlyList<StartupItemEntry> Parse(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length < 2) return [];

        var headerIndex = BuildHeaderIndex(lines[0]);

        var entries = new List<StartupItemEntry>(lines.Length - 1);
        for (var row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            var fields = ParseCsvLine(lines[row]);

            entries.Add(new StartupItemEntry
            {
                Source   = CsvField(fields, headerIndex, "Source"),
                Name     = CsvField(fields, headerIndex, "Name"),
                User     = CsvField(fields, headerIndex, "User"),
                Command  = CsvField(fields, headerIndex, "Command"),
                Location = CsvField(fields, headerIndex, "Location"),
                Enabled  = bool.TryParse(CsvField(fields, headerIndex, "Enabled"), out var en) && en,
            });
        }
        return entries;
    }

    private static Dictionary<string, int> BuildHeaderIndex(string headerLine)
    {
        var headers = ParseCsvLine(headerLine);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++) map[headers[i]] = i;
        return map;
    }

    private static string CsvField(List<string> fields, Dictionary<string, int> headerIndex, string column)
    {
        if (!headerIndex.TryGetValue(column, out var idx)) return string.Empty;
        return idx < fields.Count ? fields[idx] : string.Empty;
    }

    /// <summary>
    /// ダブルクォート + "" エスケープ対応の簡易 CSV 行パーサ。
    /// CertificatesParserService / MemoryInventoryParserService / EnvironmentVariablesParserService と同流派。
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
