using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 27_EnvironmentVariables.csv をパースする。
/// evidence_config v1.6.0+ §27 (kernel 3.0.0+) が出力する 3 列 CSV を
/// header-index lookup で読み込む (列順依存しない)。
/// CSV パーサ流派は CertificatesParserService / MemoryInventoryParserService と同じ
/// (ダブルクォート + "" エスケープ対応の private static)。
/// </summary>
public sealed class EnvironmentVariablesParserService : IEnvironmentVariablesParserService
{
    public IReadOnlyList<EnvironmentVariableEntry> Parse(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length < 2) return [];

        var headerIndex = BuildHeaderIndex(lines[0]);

        var entries = new List<EnvironmentVariableEntry>(lines.Length - 1);
        for (var row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            var fields = ParseCsvLine(lines[row]);

            entries.Add(new EnvironmentVariableEntry
            {
                Scope = CsvField(fields, headerIndex, "Scope"),
                Name  = CsvField(fields, headerIndex, "Name"),
                Value = CsvField(fields, headerIndex, "Value"),
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
    /// CertificatesParserService / MemoryInventoryParserService と同流派。
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
