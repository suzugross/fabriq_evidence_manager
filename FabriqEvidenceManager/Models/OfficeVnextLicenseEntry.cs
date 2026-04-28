namespace FabriqEvidenceManager.Models;

/// <summary>
/// 22_OfficeVnextLicenses.csv の 1 行 (= 1 vNext per-user ライセンス) を表す。
/// evidence_config.ps1 §22c (v1.5.0+) が出力する 16 列を保持。
/// 秘密情報 (RenewalToken / Signature 生値) は CSV に含まれず、
/// HardwareId は "(present)" / "" のフラグのみ。
/// </summary>
public sealed class OfficeVnextLicenseEntry
{
    /// <summary>Provisioned ステータスを示す CSV 上の正規値。</summary>
    public const string ProvisionedStatusText = "Provisioned";

    /// <summary>C:\Users\&lt;name&gt; のプロファイル名 (例: "y_suzuki")</summary>
    public string UserProfile { get; init; } = string.Empty;

    /// <summary>ライセンスファイルの親フォルダ名 (例: "5" = commercial subscription)</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>ライセンスファイル名 (数字のみ)</summary>
    public string LicenseFile { get; init; } = string.Empty;

    /// <summary>例: "Subscription"</summary>
    public string LicenseType { get; init; } = string.Empty;

    /// <summary>例: "O365BusinessRetail"</summary>
    public string ProductReleaseId { get; init; } = string.Empty;

    /// <summary>例: "Provisioned" / "" / その他</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>"True" / "False" / "" の文字列保持 (表示専用)</summary>
    public string IsTrial { get; init; } = string.Empty;

    /// <summary>例: "User"</summary>
    public string Beneficiary { get; init; } = string.Empty;

    public string LicenseId { get; init; } = string.Empty;

    public string Acid { get; init; } = string.Empty;

    public string TenantId { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    /// <summary>"(present)" / "" のフラグ (秘密情報は出力しない)</summary>
    public string HardwareIdBound { get; init; } = string.Empty;

    /// <summary>ISO 8601 文字列 (表示専用、計算には使わない)</summary>
    public string NotBefore { get; init; } = string.Empty;

    /// <summary>ISO 8601 文字列 (表示専用、計算には使わない)</summary>
    public string NotAfter { get; init; } = string.Empty;

    /// <summary>"OK" / "ParseFailed: ..." / その他</summary>
    public string ParseStatus { get; init; } = string.Empty;

    /// <summary>Status == "Provisioned" のとき true。</summary>
    public bool IsProvisioned => Status == ProvisionedStatusText;
}
