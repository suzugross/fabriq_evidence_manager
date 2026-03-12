using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// log_uploader が作成するネスト構造のエビデンスフォルダを走査する。
/// 構造: {destination}/{timestamp}_{hostname}_{mediaSerial}/evidence/{category}/...
/// 各 PC フォルダ内の evidence/ サブフォルダを既存の <see cref="EvidenceDiscoveryService"/> に委譲し、
/// 結果をマージして返す。
/// </summary>
public sealed class NestedEvidenceDiscoveryService : IEvidenceDiscoveryService
{
    private readonly EvidenceDiscoveryService _innerDiscovery;

    public NestedEvidenceDiscoveryService(EvidenceDiscoveryService innerDiscovery)
    {
        _innerDiscovery = innerDiscovery;
    }

    public Task<IReadOnlyList<PcEvidence>> DiscoverAsync(
        string evidenceRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(evidenceRootPath))
            throw new DirectoryNotFoundException($"evidence ルートが見つかりません: {evidenceRootPath}");

        // PC名+シリアル+収集日の複合キーで最終的に集約
        var mergedMap = new Dictionary<string, PcEvidence>(StringComparer.OrdinalIgnoreCase);

        // フォルダ名順ソートで同日再キッティング時に最新が後勝ち
        var pcFolders = Directory.EnumerateDirectories(evidenceRootPath)
            .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

        foreach (var pcFolder in pcFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var evidencePath = Path.Combine(pcFolder, "evidence");
            if (!Directory.Exists(evidencePath))
                continue;

            // 既存のフラット Discovery で evidence/ 内を走査
            var pcsInFolder = _innerDiscovery.DiscoverAsync(evidencePath, cancellationToken).Result;

            foreach (var pc in pcsInFolder)
            {
                var key = $"{pc.PcName}\t{pc.SerialNumber}\t{pc.CollectionDate}";

                if (mergedMap.TryGetValue(key, out var existing))
                {
                    // 同一PC・同一日が複数フォルダに存在する場合、上書き（同日再キッティング対応）
                    existing.PcInformationDirectoryPath = pc.PcInformationDirectoryPath ?? existing.PcInformationDirectoryPath;
                    existing.AutoCaptureDirectoryPath = pc.AutoCaptureDirectoryPath ?? existing.AutoCaptureDirectoryPath;
                    existing.BitLockerDirectoryPath = pc.BitLockerDirectoryPath ?? existing.BitLockerDirectoryPath;
                    existing.ChecklistFilePath = pc.ChecklistFilePath ?? existing.ChecklistFilePath;
                    existing.ExportHistoryFilePath = pc.ExportHistoryFilePath ?? existing.ExportHistoryFilePath;
                }
                else
                {
                    mergedMap[key] = pc;
                }

                // gyotaku/ → AutoCaptureDirectoryPath へのフォールバック
                var target = mergedMap[key];
                if (target.AutoCaptureDirectoryPath is null)
                {
                    var gyotakuPath = Path.Combine(evidencePath, EvidenceConstants.DirGyotaku);
                    if (Directory.Exists(gyotakuPath))
                        target.AutoCaptureDirectoryPath = gyotakuPath;
                }
            }
        }

        IReadOnlyList<PcEvidence> result = mergedMap.Values
            .OrderBy(e => e.PcName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(result);
    }
}
