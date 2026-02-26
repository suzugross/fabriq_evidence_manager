using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FabriqEvidenceManager.Models;

namespace FabriqEvidenceManager.Services.Interfaces;

/// <summary>
/// evidence ルートフォルダを走査し、PC単位のエビデンス情報一覧を構築する。
/// 各サブディレクトリ (auto_capture, bitlocker, checklist, export_history, pc_information)
/// の命名規則からPC名を特定し、同一PCのファイル群を <see cref="PcEvidence"/> に集約する。
/// </summary>
public interface IEvidenceDiscoveryService
{
    /// <summary>
    /// 指定された evidence ルートフォルダを走査し、検出された全PCのエビデンス情報を返す。
    /// </summary>
    /// <param name="evidenceRootPath">evidence ディレクトリの絶対パス</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>PC単位に集約されたエビデンス情報のリスト</returns>
    Task<IReadOnlyList<PcEvidence>> DiscoverAsync(string evidenceRootPath, CancellationToken cancellationToken = default);
}
