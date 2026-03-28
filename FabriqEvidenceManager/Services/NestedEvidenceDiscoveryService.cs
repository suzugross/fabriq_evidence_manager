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
/// log_uploader が作成するエビデンスフォルダを走査する。
///
/// 形式: {timestamp}_{PCName}_{SerialNumber}_evidence/evidence/{category}/ファイル直接配置
///   → 親フォルダ名から PC 情報を取得し、カテゴリフォルダのパスを直接割り当て。
///
/// <c>_evidence</c> サフィックスを持つフォルダのみを対象とする。
/// </summary>
public sealed class NestedEvidenceDiscoveryService : IEvidenceDiscoveryService
{
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

            var folderName = Path.GetFileName(pcFolder);

            // _evidence サフィックスを持つフォルダのみ対象
            if (!EvidencePathParser.IsNewFormatFolder(folderName))
                continue;

            var evidencePath = Path.Combine(pcFolder, "evidence");
            if (!Directory.Exists(evidencePath))
                continue;

            ProcessFolder(folderName, evidencePath, mergedMap);
        }

        IReadOnlyList<PcEvidence> result = mergedMap.Values
            .OrderBy(e => e.PcName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// フォルダを処理する。
    /// 親フォルダ名から PC名・SN・日付をパースし、evidence/ 内のカテゴリフォルダを
    /// サブディレクトリ走査なしで直接割り当てる。
    /// </summary>
    private static void ProcessFolder(
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
