using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// manifest.json (kernel/EVIDENCE_MANIFEST.md schemaVersion=1) リーダー。
/// 以下のチェックを順に実施し、不適合時は専用例外を投げる:
///   1. manifest.json 存在チェック  → ManifestNotFoundException
///   2. JSON 構文チェック             → ManifestParseException
///   3. schemaVersion == 1 チェック   → UnsupportedManifestSchemaException
///   4. manifestType == "fabriq-evidence-manifest" チェック → ManifestParseException
///   5. 本デシリアライズ              → ManifestParseException (必須欠損時)
/// </summary>
public sealed class ManifestReaderService : IManifestReaderService
{
    private const int SupportedSchemaVersion = 1;
    private const string ExpectedManifestType = "fabriq-evidence-manifest";
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new SectionStatusConverter() },
    };

    public EvidenceManifest Read(string pcInformationDirectoryPath)
    {
        var manifestPath = Path.Combine(pcInformationDirectoryPath, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new ManifestNotFoundException(manifestPath);

        string json;
        try
        {
            json = File.ReadAllText(manifestPath, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ManifestParseException(
                $"manifest.json の読み込みに失敗: {manifestPath}", ex);
        }

        // schemaVersion / manifestType を本デシリアライズ前にプリチェック。
        // 不正 manifest を typed model に流し込む前に明示エラーで止める。
        int detectedSchema;
        string? detectedType;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            detectedSchema = root.TryGetProperty("schemaVersion", out var sv) && sv.ValueKind == JsonValueKind.Number
                ? sv.GetInt32()
                : -1;
            detectedType = root.TryGetProperty("manifestType", out var mt) && mt.ValueKind == JsonValueKind.String
                ? mt.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            throw new ManifestParseException(
                $"manifest.json は有効な JSON ではありません: {manifestPath}", ex);
        }

        if (detectedSchema != SupportedSchemaVersion)
            throw new UnsupportedManifestSchemaException(detectedSchema);

        if (detectedType != ExpectedManifestType)
            throw new ManifestParseException(
                $"manifestType が不正: 期待値 \"{ExpectedManifestType}\" / " +
                $"実値 \"{detectedType ?? "(なし)"}\" ({manifestPath})");

        try
        {
            var manifest = JsonSerializer.Deserialize<EvidenceManifest>(json, JsonOptions);
            if (manifest is null)
                throw new ManifestParseException(
                    $"manifest.json が null にデシリアライズされました: {manifestPath}");
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new ManifestParseException(
                $"manifest.json のデシリアライズに失敗: {manifestPath} - {ex.Message}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ManifestParseException(
                $"manifest.json の必須フィールドが欠損: {manifestPath} - {ex.Message}", ex);
        }
    }
}

/// <summary>
/// SectionStatus の JsonConverter。
/// 既知の "Success" / "Skipped" / "Failed" / "Partial" 以外の値は
/// EVIDENCE_MANIFEST.md §4.1.3 に従い <see cref="SectionStatus.Failed"/> 扱いに正規化する。
/// </summary>
internal sealed class SectionStatusConverter : JsonConverter<SectionStatus>
{
    public override SectionStatus Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        return Enum.TryParse<SectionStatus>(s, ignoreCase: false, out var result)
            ? result
            : SectionStatus.Failed;
    }

    public override void Write(
        Utf8JsonWriter writer, SectionStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
