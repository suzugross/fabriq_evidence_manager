using System.IO;
using System.Text;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// hostlist.csv を読み込み、NewPCName をキーにした検索を提供する。
/// CSV はヘッダ行付き、カンマ区切り。BOM付きUTF-8を想定。
/// </summary>
public sealed class HostlistService : IHostlistService
{
    private readonly Dictionary<string, HostlistEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public bool IsLoaded => _entries.Count > 0;
    public int Count => _entries.Count;

    public int Load(string csvPath)
    {
        _entries.Clear();

        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2) return 0;

        var headers = ParseCsvLine(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
            headerIndex[headers[i].Trim()] = i;

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;

            var fields = ParseCsvLine(lines[row]);
            var entry = MapToEntry(fields, headerIndex);
            if (!string.IsNullOrWhiteSpace(entry.NewPCName))
                _entries[entry.NewPCName] = entry;
        }

        return _entries.Count;
    }

    public HostlistEntry? FindByPcName(string pcName) =>
        _entries.TryGetValue(pcName, out var entry) ? entry : null;

    public IReadOnlyList<HostlistEntry> GetAll() => _entries.Values.ToList();

    public void Clear() => _entries.Clear();

    // ---- private helpers ----

    private static string GetField(string[] fields, Dictionary<string, int> headerIndex, string columnName) =>
        headerIndex.TryGetValue(columnName, out var idx) && idx < fields.Length
            ? fields[idx].Trim()
            : string.Empty;

    private static HostlistEntry MapToEntry(string[] fields, Dictionary<string, int> headerIndex)
    {
        string F(string col) => GetField(fields, headerIndex, col);

        var printers = new List<PrinterExpectation>();
        for (int i = 1; i <= 10; i++)
        {
            var name = F($"Printer{i}Name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            printers.Add(new PrinterExpectation
            {
                Name = name,
                Driver = F($"Printer{i}Driver"),
                Port = F($"Printer{i}Port"),
            });
        }

        return new HostlistEntry
        {
            AdminID = F("AdminID"),
            OldPCName = F("OldPCName"),
            NewPCName = F("NewPCName"),
            EthernetIP = F("EthernetIP"),
            EthernetSubnet = F("EthernetSubnet"),
            EthernetGateway = F("EthernetGateway"),
            WifiIP = F("WifiIP"),
            WifiSubnet = F("WifiSubnet"),
            WifiGateway = F("WifiGateway"),
            DNS1 = F("DNS1"),
            DNS2 = F("DNS2"),
            DNS3 = F("DNS3"),
            DNS4 = F("DNS4"),
            Pin = F("Pin"),
            Printers = printers,
        };
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
