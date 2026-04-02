namespace FabriqEvidenceManager.Models;

public sealed class DefenderStatusData
{
    // ---- 保護状態 ----
    public bool AMServiceEnabled { get; init; }
    public bool AntispywareEnabled { get; init; }
    public bool AntivirusEnabled { get; init; }
    public bool RealTimeProtectionEnabled { get; init; }
    public bool BehaviorMonitorEnabled { get; init; }
    public bool IoavProtectionEnabled { get; init; }
    public bool NISEnabled { get; init; }
    public bool OnAccessProtectionEnabled { get; init; }

    // ---- バージョン情報 ----
    public string AMEngineVersion { get; init; } = string.Empty;
    public string AMProductVersion { get; init; } = string.Empty;
    public string AntivirusSignatureVersion { get; init; } = string.Empty;
    public string AntivirusSignatureLastUpdated { get; init; } = string.Empty;

    // ---- スキャン情報 ----
    public string QuickScanEndTime { get; init; } = string.Empty;
    public string FullScanEndTime { get; init; } = string.Empty;
}
