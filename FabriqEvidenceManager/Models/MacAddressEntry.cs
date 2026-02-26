namespace FabriqEvidenceManager.Models;

/// <summary>
/// 1つのネットワークアダプタのMAC情報を表す。
/// 06_MacAddress.txt の1ブロック分に対応。
/// </summary>
public sealed class MacAddressEntry
{
    /// <summary>接続名 (例: "イーサネット", "Wi-Fi")</summary>
    public required string ConnectionName { get; init; }

    /// <summary>アダプタ名 (例: "Intel(R) Ethernet Connection (16) I219-LM")</summary>
    public required string AdapterName { get; init; }

    /// <summary>MACアドレス (例: "BC-FC-E7-44-35-DB")</summary>
    public required string PhysicalAddress { get; init; }

    /// <summary>接続状態 (例: "Up", "Disconnected")</summary>
    public required string Status { get; init; }
}
