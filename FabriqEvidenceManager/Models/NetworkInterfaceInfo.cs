namespace FabriqEvidenceManager.Models;

/// <summary>
/// 03_NetworkConfig.txt から抽出したネットワークインターフェース情報。
/// </summary>
public sealed class NetworkInterfaceInfo
{
    /// <summary>インターフェース名 (例: "イーサネット", "Wi-Fi")</summary>
    public required string InterfaceName { get; init; }

    /// <summary>IPv4アドレス</summary>
    public string IPv4Address { get; init; } = string.Empty;

    /// <summary>サブネットマスク</summary>
    public string SubnetMask { get; init; } = string.Empty;

    /// <summary>デフォルトゲートウェイ</summary>
    public string DefaultGateway { get; init; } = string.Empty;

    /// <summary>DNSサーバー一覧 (ソート済み)</summary>
    public List<string> DnsServers { get; init; } = [];

    /// <summary>Ethernet系インターフェースかどうか</summary>
    public bool IsEthernet =>
        InterfaceName.Contains("イーサネット", StringComparison.OrdinalIgnoreCase)
        || InterfaceName.Contains("Ethernet", StringComparison.OrdinalIgnoreCase);

    /// <summary>Wi-Fi系インターフェースかどうか</summary>
    public bool IsWifi =>
        InterfaceName.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
        || InterfaceName.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
        || InterfaceName.Contains("ワイヤレス", StringComparison.OrdinalIgnoreCase);
}
