namespace FabriqEvidenceManager.Models;

/// <summary>
/// 04_Printers.txt から抽出したプリンタ情報。
/// 行形式: Name=xxx|Driver=xxx|Port=xxx
/// </summary>
public sealed class PrinterEntry
{
    public required string Name { get; init; }
    public string Driver { get; init; } = string.Empty;
    public string Port { get; init; } = string.Empty;
}
