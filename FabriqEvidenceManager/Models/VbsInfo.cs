using System.Collections.Generic;
using System.Linq;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// Virtualization-Based Security / HVCI / Credential Guard 状態 (evidence_config §23c)。
/// Win32_DeviceGuard の出力に対応。
/// </summary>
public sealed class VbsInfo
{
    public const string HvciServiceName = "HVCI";
    public const string CredentialGuardServiceName = "Credential Guard";

    /// <summary>"Running" / "Configured but not running" / "Not running" / "Unknown"</summary>
    public string VbsStatusText { get; init; } = string.Empty;

    /// <summary>0 = Not running / 1 = Configured but not running / 2 = Running</summary>
    public int? VbsStatusCode { get; init; }

    /// <summary>例: ["HVCI"] / ["Credential Guard", "HVCI"]</summary>
    public IReadOnlyList<string> SecurityServicesRunning { get; init; } = [];
    public IReadOnlyList<string> SecurityServicesConfigured { get; init; } = [];

    /// <summary>"Off" / "Audit" / "Enforced" / "Unknown"</summary>
    public string CodeIntegrityEnforcementText { get; init; } = string.Empty;
    public int? CodeIntegrityEnforcementCode { get; init; }

    public string UsermodeCodeIntegrityEnforcementText { get; init; } = string.Empty;
    public int? UsermodeCodeIntegrityEnforcementCode { get; init; }

    /// <summary>raw 数値コード列 (例: [1, 3, 4, 5, 7, 8])</summary>
    public IReadOnlyList<int> AvailableSecurityProperties { get; init; } = [];
    public IReadOnlyList<int> RequiredSecurityProperties { get; init; } = [];

    /// <summary>SecurityServicesRunning に "HVCI" を含むか。</summary>
    public bool HvciRunning => SecurityServicesRunning.Contains(HvciServiceName);

    /// <summary>SecurityServicesRunning に "Credential Guard" を含むか。</summary>
    public bool CredentialGuardRunning => SecurityServicesRunning.Contains(CredentialGuardServiceName);

    /// <summary>VbsStatusCode == 2 のとき true。</summary>
    public bool IsVbsRunning => VbsStatusCode == 2;
}
