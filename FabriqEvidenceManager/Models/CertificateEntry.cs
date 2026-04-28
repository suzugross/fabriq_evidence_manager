using System;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// 25_Certificates.csv の 1 行 (= 1 証明書) を表す。
/// evidence_config.ps1 §25 (v1.4.0+) が出力する 10 列を保持。
/// 4 ストア統合 (LocalMachine\My / Root / CA / CurrentUser\My) を Store 列で識別する。
/// 秘密鍵そのものは出力されず、HasPrivateKey フラグのみ。
/// </summary>
public sealed class CertificateEntry
{
    /// <summary>例: "LocalMachine\\Root" / "CurrentUser\\My"</summary>
    public string Store { get; init; } = string.Empty;

    /// <summary>例: "CN=DigiCert Trusted Root G4, OU=..., O=DigiCert Inc, C=US"</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>発行者 (自己署名なら Subject と同一)</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>40 文字 hex の SHA-1 thumbprint</summary>
    public string Thumbprint { get; init; } = string.Empty;

    /// <summary>有効期間 開始 (parse 失敗時 null)</summary>
    public DateTime? NotBefore { get; init; }

    /// <summary>有効期間 終了 (parse 失敗時 null)。期限警告判定の基準。</summary>
    public DateTime? NotAfter { get; init; }

    public bool HasPrivateKey { get; init; }

    /// <summary>"; " 区切り EKU リスト (例: "クライアント認証; コード署名")</summary>
    public string EnhancedKeyUsageList { get; init; } = string.Empty;

    public string FriendlyName { get; init; } = string.Empty;

    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>
    /// NotAfter から referenceDate までの日数。NotAfter null 時は null。
    /// 負数は期限切れ。
    /// </summary>
    public int? DaysToExpiry(DateTime referenceDate)
    {
        if (NotAfter is null) return null;
        return (int)(NotAfter.Value.Date - referenceDate.Date).TotalDays;
    }
}
