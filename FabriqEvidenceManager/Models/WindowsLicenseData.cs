using System.Collections.Generic;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 21_WindowsLicense.txt のパース結果を表す集約モデル。
/// evidence_config.ps1 §21 が出力する 3 ブロック
/// (SoftwareLicensingProduct / SoftwareLicensingService / slmgr /dlv raw) に対応。
/// </summary>
public sealed class WindowsLicenseData
{
    /// <summary>インストール済み Windows プロダクト一覧。プロダクトキー未検出時は空。</summary>
    public required IReadOnlyList<WindowsLicensingProduct> Products { get; init; }

    /// <summary>
    /// マシン全体のライセンスサービス情報。
    /// SoftwareLicensingService 取得が失敗した [WARN] ケースでは null。
    /// </summary>
    public WindowsLicensingService? Service { get; init; }

    /// <summary>slmgr /dlv の人間用 raw 出力（インデント込み・空可）。</summary>
    public string SlmgrDlvRaw { get; init; } = string.Empty;
}

/// <summary>
/// SoftwareLicensingProduct (per-product) ブロックの 1 エントリ。
/// </summary>
public sealed class WindowsLicensingProduct
{
    /// <summary>
    /// 正常状態 (Licensed) を示す LicenseStatus コード。
    /// 監査表示で「Licensed 以外を赤背景」判定する際の真の基準値。
    /// </summary>
    public const int LicensedStatusCode = 1;

    /// <summary>例: "Windows(R), Professional edition"</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>例: "Windows(R) Operating System, VOLUME_KMSCLIENT channel"</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>slmgr 由来の数値コード (0..6 が既知、未知値は未マップで保持)。</summary>
    public int LicenseStatusCode { get; init; }

    /// <summary>例: "Notification Mode" / "Licensed"。未知 code は "Unknown"。</summary>
    public string LicenseStatusText { get; init; } = string.Empty;

    /// <summary>例: "T83GX"</summary>
    public string PartialProductKey { get; init; } = string.Empty;

    /// <summary>例: "Professional"</summary>
    public string LicenseFamily { get; init; } = string.Empty;

    /// <summary>例: "Volume:GVLK" / "OEM:NONSLP" / "Retail" 等</summary>
    public string ProductKeyChannel { get; init; } = string.Empty;

    /// <summary>"GracePeriodRemaining: N minutes" 行があれば N (分)、無ければ null。</summary>
    public int? GracePeriodMinutes { get; init; }

    /// <summary>"KMS (configured): host" 行が出力されていればその値、無ければ null。</summary>
    public string? KmsConfigured { get; init; }

    /// <summary>"KMS (discovered): host:port" 行が出力されていればその値、無ければ null。</summary>
    public string? KmsDiscovered { get; init; }
}

/// <summary>
/// SoftwareLicensingService (machine-wide) ブロック。
/// </summary>
public sealed class WindowsLicensingService
{
    public string ClientMachineID { get; init; } = string.Empty;

    /// <summary>明示設定された KMS ホスト。未設定時は空文字列。</summary>
    public string KeyManagementServiceMachine { get; init; } = string.Empty;

    /// <summary>KMS ポート。未設定時は空文字列。型は表示用途のみのため string で保持。</summary>
    public string KeyManagementServicePort { get; init; } = string.Empty;

    public string OA3xOriginalProductKey { get; init; } = string.Empty;
    public string OA3xOriginalProductKeyDescription { get; init; } = string.Empty;
    public string PolicyCacheRefreshRequired { get; init; } = string.Empty;
}
