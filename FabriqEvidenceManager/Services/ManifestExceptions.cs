using System;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// manifest.json が pc_information ディレクトリに存在しない。
/// fabriq kernel 2.2.2+ / evidence_config 1.3.0+ 由来でない可能性が高い
/// (silent fallback はしない方針 — EVIDENCE_MANIFEST.md §4.1.1)。
/// </summary>
public sealed class ManifestNotFoundException : Exception
{
    /// <summary>探したパス</summary>
    public string Path { get; }

    public ManifestNotFoundException(string path)
        : base($"manifest.json が見つかりません: {path}")
    {
        Path = path;
    }
}

/// <summary>
/// 未対応の schemaVersion を検知。
/// 前方互換ルール (EVIDENCE_MANIFEST.md §4.1.1): 警告 + 停止。
/// silent な部分動作は禁止。
/// </summary>
public sealed class UnsupportedManifestSchemaException : Exception
{
    /// <summary>本アプリがサポートする schemaVersion</summary>
    public const int ExpectedSchemaVersion = 1;

    /// <summary>manifest 上で検出された schemaVersion (未取得時は -1)</summary>
    public int DetectedSchemaVersion { get; }

    public UnsupportedManifestSchemaException(int detectedSchemaVersion)
        : base($"未対応の manifest schemaVersion: {detectedSchemaVersion} " +
               $"(このアプリは schemaVersion={ExpectedSchemaVersion} のみサポート)")
    {
        DetectedSchemaVersion = detectedSchemaVersion;
    }
}

/// <summary>
/// manifest.json の JSON 不正 / manifestType 不一致 / 必須フィールド欠損。
/// </summary>
public sealed class ManifestParseException : Exception
{
    public ManifestParseException(string message) : base(message) { }
    public ManifestParseException(string message, Exception innerException)
        : base(message, innerException) { }
}
