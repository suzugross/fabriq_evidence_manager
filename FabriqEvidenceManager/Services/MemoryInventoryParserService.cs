using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 29_MemorySlots.csv / 29b_MemoryArraySummary.csv をパースする。
/// evidence_config v1.6.0+ §29 (kernel 3.0.0+) は 1 セクションで 2 ファイル出力。
/// CSV パーサ流派は CertificatesParserService と同じ (ダブルクォート + ""
/// エスケープ対応の private static、ヘッダ列順非依存の header-index lookup)。
/// </summary>
public sealed class MemoryInventoryParserService : IMemoryInventoryParserService
{
    public IReadOnlyList<MemorySlotEntry> ParseSlots(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length < 2) return [];

        var headerIndex = BuildHeaderIndex(lines[0]);

        var entries = new List<MemorySlotEntry>(lines.Length - 1);
        for (var row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            var fields = ParseCsvLine(lines[row]);

            entries.Add(new MemorySlotEntry
            {
                BankLabel               = CsvField(fields, headerIndex, "BankLabel"),
                DeviceLocator           = CsvField(fields, headerIndex, "DeviceLocator"),
                CapacityGB              = ParseIntNullable(CsvField(fields, headerIndex, "Capacity_GB")),
                SpeedMhz                = ParseIntNullable(CsvField(fields, headerIndex, "Speed_MHz")),
                ConfiguredClockSpeedMhz = ParseIntNullable(CsvField(fields, headerIndex, "ConfiguredClockSpeed_MHz")),
                ConfiguredVoltageMv     = ParseIntNullable(CsvField(fields, headerIndex, "ConfiguredVoltage_mV")),
                Manufacturer            = CsvField(fields, headerIndex, "Manufacturer"),
                PartNumber              = CsvField(fields, headerIndex, "PartNumber"),
                SerialNumber            = CsvField(fields, headerIndex, "SerialNumber"),
                FormFactor              = CsvField(fields, headerIndex, "FormFactor"),
                SmbiosMemoryType        = CsvField(fields, headerIndex, "SMBIOSMemoryType"),
                DataWidthBit            = ParseIntNullable(CsvField(fields, headerIndex, "DataWidth_bit")),
                TotalWidthBit           = ParseIntNullable(CsvField(fields, headerIndex, "TotalWidth_bit")),
            });
        }
        return entries;
    }

    public IReadOnlyList<MemoryArraySummaryEntry> ParseArraySummary(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length < 2) return [];

        var headerIndex = BuildHeaderIndex(lines[0]);

        var entries = new List<MemoryArraySummaryEntry>(lines.Length - 1);
        for (var row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            var fields = ParseCsvLine(lines[row]);

            entries.Add(new MemoryArraySummaryEntry
            {
                Tag                   = CsvField(fields, headerIndex, "Tag"),
                Location              = CsvField(fields, headerIndex, "Location"),
                Use                   = CsvField(fields, headerIndex, "Use"),
                MemoryErrorCorrection = CsvField(fields, headerIndex, "MemoryErrorCorrection"),
                MaxCapacityGB         = ParseIntNullable(CsvField(fields, headerIndex, "MaxCapacity_GB")),
                MemoryDevices         = ParseIntNullable(CsvField(fields, headerIndex, "MemoryDevices")),
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

    private static int? ParseIntNullable(string value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    /// <summary>
    /// ダブルクォート + "" エスケープ対応の簡易 CSV 行パーサ。
    /// CertificatesParserService / OfficeLicenseParserService と同流派。
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
