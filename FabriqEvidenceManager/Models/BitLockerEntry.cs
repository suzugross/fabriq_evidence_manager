namespace FabriqEvidenceManager.Models;

/// <summary>
/// 1ドライブ分のBitLocker回復キー情報を表す。
/// bitlocker/{pcName}_{drive}.txt に対応。
/// </summary>
public sealed class BitLockerEntry
{
    /// <summary>ドライブレター (例: "C")</summary>
    public required string DriveLetter { get; init; }

    /// <summary>回復キーID / Identifier (GUID文字列)</summary>
    public required string KeyId { get; init; }

    /// <summary>回復パスワード (例: "237831-141273-279554-...")</summary>
    public required string RecoveryPassword { get; init; }
}
