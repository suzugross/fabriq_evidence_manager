namespace FabriqEvidenceManager.Models;

/// <summary>
/// TPM 状態 (evidence_config §23a)。Get-Tpm の出力に対応。
/// 個別行が取れない場合は対応プロパティが null になる。
/// セクションヘッダ直下の行が "(Get-Tpm not available: ...)" だった場合は
/// <see cref="SecurityBaselineData.Tpm"/> 自体が null になり、本クラスは生成されない。
/// </summary>
public sealed class TpmInfo
{
    public bool? Present { get; init; }
    public bool? Ready { get; init; }
    public bool? Enabled { get; init; }
    public bool? Activated { get; init; }
    public bool? Owned { get; init; }

    /// <summary>例: "1398033696"</summary>
    public string ManufacturerId { get; init; } = string.Empty;

    public string ManufacturerVersion { get; init; } = string.Empty;

    /// <summary>"Full" / "Delegated" 等</summary>
    public string ManagedAuthLevel { get; init; } = string.Empty;

    /// <summary>"(present)" / "(absent)" のフラグ表示</summary>
    public string OwnerAuth { get; init; } = string.Empty;

    public bool? LockedOut { get; init; }
    public int? LockoutCount { get; init; }
    public int? LockoutMax { get; init; }
}
