using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FabriqEvidenceManager.Models;

/// <summary>
/// pc_information/&lt;collectionDir&gt;/manifest.json のデシリアライズ結果。
/// schemaVersion=1 (kernel 2.2.2 / evidence_config 1.3.0+) 公開契約に準拠。
/// 詳細は kernel/EVIDENCE_MANIFEST.md §3.1。
/// </summary>
public sealed class EvidenceManifest
{
    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("manifestType")]
    public required string ManifestType { get; init; }

    [JsonPropertyName("evidenceConfigVersion")]
    public required string EvidenceConfigVersion { get; init; }

    [JsonPropertyName("fabriqKernelVersion")]
    public required string FabriqKernelVersion { get; init; }

    [JsonPropertyName("collectedAt")]
    public required DateTimeOffset CollectedAt { get; init; }

    [JsonPropertyName("computerName")]
    public required string ComputerName { get; init; }

    [JsonPropertyName("hardwareUniqueId")]
    public required string HardwareUniqueId { get; init; }

    [JsonPropertyName("selectedNewPcName")]
    public required string SelectedNewPcName { get; init; }

    [JsonPropertyName("workerName")]
    public string? WorkerName { get; init; }

    [JsonPropertyName("sections")]
    public required IReadOnlyList<ManifestSection> Sections { get; init; }

    [JsonPropertyName("summary")]
    public required EvidenceManifestSummary Summary { get; init; }
}

/// <summary>
/// manifest summary (集計値)。EVIDENCE_MANIFEST.md §3.4。
/// 不変条件: success + skipped + failed + partial == sectionCount。
/// </summary>
public sealed class EvidenceManifestSummary
{
    [JsonPropertyName("sectionCount")]
    public int SectionCount { get; init; }

    [JsonPropertyName("successCount")]
    public int SuccessCount { get; init; }

    [JsonPropertyName("skippedCount")]
    public int SkippedCount { get; init; }

    [JsonPropertyName("failedCount")]
    public int FailedCount { get; init; }

    [JsonPropertyName("partialCount")]
    public int PartialCount { get; init; }
}
