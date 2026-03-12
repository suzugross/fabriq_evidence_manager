namespace FabriqEvidenceManager.Models;

/// <summary>
/// 05_BitLocker.txt から抽出したボリューム単位のBitLockerステータス。
/// </summary>
public sealed class BitLockerVolumeStatus
{
    /// <summary>ドライブレター (例: "C")</summary>
    public required string DriveLetter { get; init; }

    /// <summary>ボリュームラベル (例: "OperatingSystem", "Data")</summary>
    public string VolumeLabel { get; init; } = string.Empty;

    /// <summary>変換状態 (例: "FullyEncrypted", "FullyDecrypted")</summary>
    public string ConversionStatus { get; init; } = string.Empty;

    /// <summary>暗号化割合 (例: "100%")</summary>
    public string EncryptionPercentage { get; init; } = string.Empty;

    /// <summary>暗号化方式 (例: "XtsAes128")</summary>
    public string EncryptionMethod { get; init; } = string.Empty;

    /// <summary>保護状態 (例: "On", "Off")</summary>
    public string ProtectionStatus { get; init; } = string.Empty;

    /// <summary>BitLockerが有効 (Protection Status = On) かどうか</summary>
    public bool IsProtected =>
        ProtectionStatus.Equals("On", StringComparison.OrdinalIgnoreCase);
}
