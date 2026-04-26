using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// pc_information/&lt;collectionDir&gt;/manifest.json を読み込み、
/// <see cref="EvidenceManifest"/> に構造化する。
/// fabriq kernel 2.2.2+ の公開契約 (kernel/EVIDENCE_MANIFEST.md schemaVersion=1) に準拠。
/// </summary>
public interface IManifestReaderService
{
    /// <summary>
    /// 指定ディレクトリ直下の manifest.json を読み込む。
    /// </summary>
    /// <param name="pcInformationDirectoryPath">manifest.json が配置されているディレクトリ</param>
    /// <returns>パース結果</returns>
    /// <exception cref="ManifestNotFoundException">manifest.json 不在</exception>
    /// <exception cref="UnsupportedManifestSchemaException">未対応の schemaVersion (前方互換: 警告 + 停止)</exception>
    /// <exception cref="ManifestParseException">JSON 不正 / manifestType 不一致 / 必須フィールド欠損</exception>
    EvidenceManifest Read(string pcInformationDirectoryPath);
}
