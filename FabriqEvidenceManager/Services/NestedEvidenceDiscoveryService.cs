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
///
/// 旧形式: {timestamp}_{COMPUTERNAME}_{mediaSerial}/evidence/{category}/{date_sn_pc}/...
///   → カテゴリ内にサブフォルダが存在し、既存の <see cref="EvidenceDiscoveryService"/> に委譲。
///
/// 新形式: {timestamp}_{PCName}_{SerialNumber}_evidence/evidence/{category}/ファイル直接配置
///   → 親フォルダ名から PC 情報を取得し、カテゴリフォルダのパスを直接割り当て。
///
/// <c>_evidence</c> サフィックスの有無で新旧を自動判別し、両方を同一リストにマージして返す。
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

            var folderName = Path.GetFileName(pcFolder);

            if (EvidencePathParser.IsNewFormatFolder(folderName))
            {
                // ====== 新形式: 親フォルダ名から PC 情報を取得 ======
                ProcessNewFormat(folderName, evidencePath, mergedMap);
            }
            else
            {
                // ====== 旧形式: フラット Discovery に委譲 ======
                ProcessLegacyFormat(evidencePath, mergedMap, cancellationToken);
            }
        }

        IReadOnlyList<PcEvidence> result = mergedMap.Values
            .OrderBy(e => e.PcName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// 新形式フォルダを処理する。
    /// 親フォルダ名から PC名・SN・日付をパースし、evidence/ 内のカテゴリフォルダを
    /// サブディレクトリ走査なしで直接割り当てる。
    /// </summary>
    private static void ProcessNewFormat(
        string folderName,
        string evidencePath,
        Dictionary<string, PcEvidence> mergedMap)
    {
        if (!EvidencePathParser.TryParseNewFormat(folderName, out var pcName, out var serial, out var date))
            return;

        var key = $"{pcName}\t{serial}\t{date}";
        var pc = GetOrMerge(mergedMap, key, pcName, serial, date);

        // ----- pc_information (フラット: ファイル直接配置) -----
        var pcInfoPath = Path.Combine(evidencePath, EvidenceConstants.DirPcInformation);
        if (Directory.Exists(pcInfoPath))
            pc.PcInformationDirectoryPath = pcInfoPath;

        // ----- auto_capture (フラット: ファイル直接配置) -----
        var autoCapturePath = Path.Combine(evidencePath, EvidenceConstants.DirAutoCapture);
        if (Directory.Exists(autoCapturePath))
            pc.AutoCaptureDirectoryPath = autoCapturePath;

        // ----- gyotaku → AutoCaptureDirectoryPath フォールバック -----
        if (pc.AutoCaptureDirectoryPath is null)
        {
            var gyotakuPath = Path.Combine(evidencePath, EvidenceConstants.DirGyotaku);
            if (Directory.Exists(gyotakuPath))
                pc.AutoCaptureDirectoryPath = gyotakuPath;
        }

        // ----- bitlocker (フラット: ファイル直接配置) -----
        var bitlockerPath = Path.Combine(evidencePath, EvidenceConstants.DirBitLocker);
        if (Directory.Exists(bitlockerPath))
            pc.BitLockerDirectoryPath = bitlockerPath;

        // ----- checklist (ファイル単体を探索) -----
        var checklistDir = Path.Combine(evidencePath, EvidenceConstants.DirChecklist);
        if (Directory.Exists(checklistDir))
        {
            var checklistFile = Directory.EnumerateFiles(checklistDir)
                .Where(f => Path.GetFileName(f).StartsWith(EvidenceConstants.PrefixChecklist, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .LastOrDefault(); // 名前順で最新を採用
            if (checklistFile is not null)
                pc.ChecklistFilePath = checklistFile;
        }

        // ----- export_history (ファイル単体を探索) -----
        var historyDir = Path.Combine(evidencePath, EvidenceConstants.DirExportHistory);
        if (Directory.Exists(historyDir))
        {
            var historyFile = Directory.EnumerateFiles(historyDir)
                .Where(f => Path.GetFileName(f).StartsWith(EvidenceConstants.PrefixExportHistory, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .LastOrDefault(); // 名前順で最新を採用
            if (historyFile is not null)
                pc.ExportHistoryFilePath = historyFile;
        }
    }

    /// <summary>
    /// 旧形式フォルダを処理する。
    /// 既存のフラット Discovery に委譲し、結果を mergedMap にマージする。
    /// </summary>
    private void ProcessLegacyFormat(
        string evidencePath,
        Dictionary<string, PcEvidence> mergedMap,
        CancellationToken cancellationToken)
    {
        var pcsInFolder = _innerDiscovery.DiscoverAsync(evidencePath, cancellationToken).Result;

        foreach (var pc in pcsInFolder)
        {
            // 親フォルダが異なれば別エントリとして扱う
            var key = $"{pc.PcName}\t{pc.SerialNumber}\t{pc.CollectionDate}\t{evidencePath}";

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

    /// <summary>
    /// 複合キーで既存の PcEvidence を取得するか、新規作成して mergedMap に登録する。
    /// </summary>
    private static PcEvidence GetOrMerge(
        Dictionary<string, PcEvidence> map,
        string key,
        string pcName,
        string serial,
        string collectionDate)
    {
        if (map.TryGetValue(key, out var existing))
            return existing;

        var pc = new PcEvidence
        {
            PcName = pcName,
            SerialNumber = serial,
            CollectionDate = collectionDate,
        };
        map[key] = pc;
        return pc;
    }
}
