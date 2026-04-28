namespace FabriqEvidenceManager.Models;

/// <summary>
/// 23_SecurityBaseline.txt のパース結果を表す集約モデル。
/// evidence_config.ps1 §23 (v1.4.0+) が出力する 5 ブロック構成
/// (TPM / Secure Boot / VBS / LSA / BIOS) に対応。
/// 各 probe は inner try/catch で個別退避されるため、セクション自体が成功でも
/// 個別ブロックが取得不能な場合がある。取得不能ブロックは対応プロパティが null になる。
/// </summary>
public sealed class SecurityBaselineData
{
    /// <summary>TPM 状態。"(Get-Tpm not available: ...)" 時 null。</summary>
    public TpmInfo? Tpm { get; init; }

    /// <summary>
    /// Secure Boot 有効/無効。legacy BIOS / 取得失敗時 null。
    /// </summary>
    public bool? SecureBootEnabled { get; init; }

    /// <summary>VBS / HVCI / Credential Guard 状態。"(Win32_DeviceGuard query failed: ...)" 時 null。</summary>
    public VbsInfo? Vbs { get; init; }

    /// <summary>LSA Protection 状態。"(LSA Protection registry query failed: ...)" 時 null。</summary>
    public LsaProtectionInfo? Lsa { get; init; }

    /// <summary>BIOS / Firmware 情報。"(Win32_BIOS query failed: ...)" 時 null。</summary>
    public BiosInfo? Bios { get; init; }
}
