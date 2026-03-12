namespace FabriqEvidenceManager.Models;

/// <summary>
/// hostlist.csv の1行分を表すモデル。
/// fabriq のキッティング対象PCの期待値（Expected）として使用する。
/// </summary>
public sealed class HostlistEntry
{
    // ---- 識別情報 ----
    public string AdminID { get; init; } = string.Empty;
    public string OldPCName { get; init; } = string.Empty;
    public string NewPCName { get; init; } = string.Empty;

    // ---- ネットワーク (Ethernet) ----
    public string EthernetIP { get; init; } = string.Empty;
    public string EthernetSubnet { get; init; } = string.Empty;
    public string EthernetGateway { get; init; } = string.Empty;

    // ---- ネットワーク (Wi-Fi) ----
    public string WifiIP { get; init; } = string.Empty;
    public string WifiSubnet { get; init; } = string.Empty;
    public string WifiGateway { get; init; } = string.Empty;

    // ---- DNS ----
    public string DNS1 { get; init; } = string.Empty;
    public string DNS2 { get; init; } = string.Empty;
    public string DNS3 { get; init; } = string.Empty;
    public string DNS4 { get; init; } = string.Empty;

    // ---- PIN ----
    public string Pin { get; init; } = string.Empty;

    // ---- プリンタ (最大10台) ----
    public List<PrinterExpectation> Printers { get; init; } = [];

    /// <summary>
    /// DNS1〜DNS4 の非空値をソート済みリストとして返す。
    /// 突合時に順序に依存しない比較を行うため。
    /// </summary>
    public List<string> GetDnsList() =>
        new[] { DNS1, DNS2, DNS3, DNS4 }
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Order()
            .ToList();
}

/// <summary>
/// hostlist.csv のプリンタ1台分の期待値。
/// </summary>
public sealed class PrinterExpectation
{
    public required string Name { get; init; }
    public required string Driver { get; init; }
    public required string Port { get; init; }
}
