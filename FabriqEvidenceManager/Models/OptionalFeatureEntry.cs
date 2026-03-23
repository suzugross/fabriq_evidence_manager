namespace FabriqEvidenceManager.Models;

/// <summary>
/// 13_OptionalFeatures.csv から抽出されたWindowsオプション機能。
/// </summary>
public sealed class OptionalFeatureEntry
{
    public required string FeatureName { get; init; }
    public string State { get; init; } = string.Empty;
}
