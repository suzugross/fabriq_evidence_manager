using System.Collections.Generic;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 22_OfficeLicense.txt と 22_OfficeVnextLicenses.csv のパース結果を表す集約モデル。
/// evidence_config.ps1 §22 (v1.5.0+) が出力する 4 ブロック構成
/// (22a Click-to-Run config / 22b OSPP path+/dstatus raw / 22c vNext per-user /
/// 22d INTERPRETATION) に対応。
/// </summary>
public sealed class OfficeLicenseData
{
    /// <summary>
    /// Office がインストール済みか。
    /// C2R config 検出 OR OSPP.vbs 検出 のいずれかで true。両方未検出時 false。
    /// </summary>
    public bool IsOfficeInstalled { get; init; }

    /// <summary>
    /// Click-to-Run レジストリ設定。MSI-based / 未インストール時は null。
    /// </summary>
    public OfficeClickToRunConfig? ClickToRun { get; init; }

    /// <summary>
    /// 検出された OSPP.vbs の絶対パス。OSPP.vbs not found 時は null。
    /// </summary>
    public string? OsppPath { get; init; }

    /// <summary>
    /// OSPP /dstatus でパースされた製品ライセンス一覧。空可。
    /// </summary>
    public required IReadOnlyList<OfficeProductLicense> Products { get; init; }

    /// <summary>
    /// cscript OSPP.vbs /dstatus の raw 出力（インデント込み・空可）。
    /// </summary>
    public string OsppDstatusRaw { get; init; } = string.Empty;

    /// <summary>
    /// 22_OfficeVnextLicenses.csv のパース結果。CSV 不在時は空配列。
    /// (v1.5.0+ §22c)
    /// </summary>
    public IReadOnlyList<OfficeVnextLicenseEntry> VnextEntries { get; init; } = [];

    /// <summary>
    /// 22a Click-to-Run の "DetectedAsSubscription:" 行 (= Test-OfficeSubscriptionSku 結果)。
    /// 旧 evidence (v1.4 以前) または C2R 未検出時は null。
    /// </summary>
    public bool? IsSubscriptionDetected { get; init; }

    /// <summary>
    /// 22d INTERPRETATION の "OSPP shows Grace/Notify:" 行。
    /// 旧 evidence (INTERPRETATION ブロック不在) では null。
    /// </summary>
    public bool? OsppShowsGrace { get; init; }

    /// <summary>
    /// 22d INTERPRETATION の "CONCLUSION:" 行 (接頭辞除去後の本文)。
    /// INTERPRETATION ブロック不在時は null。
    /// 例: "LICENSED (M365 subscription, 2 Provisioned vNext license(s))."
    /// </summary>
    public string? InterpretationConclusion { get; init; }

    /// <summary>
    /// 22d INTERPRETATION の "vNext Provisioned:" 行 (Status=Provisioned の件数)。
    /// INTERPRETATION ブロック不在時は null。VnextEntries.Count(e =&gt; e.IsProvisioned) と
    /// 一致するはずだが、二重ソースで保持し evidence の真値を優先する。
    /// </summary>
    public int? ProvisionedCount { get; init; }
}

/// <summary>
/// HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration の主要値。
/// </summary>
public sealed class OfficeClickToRunConfig
{
    /// <summary>例: "ProPlus2024Volume" / "O365ProPlusRetail"</summary>
    public string ProductReleaseIds { get; init; } = string.Empty;

    /// <summary>例: "16.0.17932.20742"</summary>
    public string VersionToReport { get; init; } = string.Empty;

    /// <summary>例: "x64"</summary>
    public string Platform { get; init; } = string.Empty;

    public string CDNBaseUrl { get; init; } = string.Empty;

    /// <summary>例: "MonthlyEnterprise" / Volume チャネル URL</summary>
    public string UpdateChannel { get; init; } = string.Empty;

    /// <summary>例: "Production::LTSC2024"</summary>
    public string AudienceData { get; init; } = string.Empty;

    /// <summary>例: "ja-jp"</summary>
    public string ClientCulture { get; init; } = string.Empty;
}

/// <summary>
/// OSPP /dstatus の 1 製品ブロック。
/// </summary>
public sealed class OfficeProductLicense
{
    /// <summary>正常状態を示す LICENSE STATUS テキスト。M8c 着色判定の真の基準。</summary>
    public const string LicensedStatusText = "LICENSED";

    /// <summary>例: "00502-40000-00000-AA709"</summary>
    public string ProductId { get; init; } = string.Empty;

    public string SkuId { get; init; } = string.Empty;

    /// <summary>例: "Office 24, Office24ProPlus2024VL_KMS_Client_AE edition"</summary>
    public string LicenseName { get; init; } = string.Empty;

    /// <summary>例: "Office 24, VOLUME_KMSCLIENT channel"</summary>
    public string LicenseDescription { get; init; } = string.Empty;

    /// <summary>
    /// LICENSE STATUS から `---` マーカーを除去した値。
    /// 例: "LICENSED" / "OOB_GRACE" / "NOTIFICATIONS"。
    /// </summary>
    public string LicenseStatusText { get; init; } = string.Empty;

    /// <summary>例: "0x4004F00C" / 空可</summary>
    public string ErrorCode { get; init; } = string.Empty;

    public string ErrorDescription { get; init; } = string.Empty;

    /// <summary>例: "29 days  (43197 minute(s) before expiring)" / 空可</summary>
    public string RemainingGrace { get; init; } = string.Empty;

    /// <summary>例: "GCVGB"（プロダクトキー末尾 5 文字）</summary>
    public string LastFiveCharsProductKey { get; init; } = string.Empty;

    /// <summary>"KMS machine name from DNS:" 行の値（DNS 自動探索失敗時は空）</summary>
    public string KmsMachineName { get; init; } = string.Empty;

    /// <summary>LICENSE STATUS が "LICENSED" の場合に true。</summary>
    public bool IsLicensed => LicenseStatusText == LicensedStatusText;
}
