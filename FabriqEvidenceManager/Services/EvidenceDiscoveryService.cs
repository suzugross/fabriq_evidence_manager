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
/// evidence ルートフォルダを走査し、PC単位のエビデンス情報一覧を構築する。
/// 各サブカテゴリ (auto_capture, bitlocker, pc_information, checklist, export_history) を
/// 順に走査し、EvidencePathParser で PC名を抽出して同一PC名を集約する。
/// </summary>
public sealed class EvidenceDiscoveryService : IEvidenceDiscoveryService
{
    public Task<IReadOnlyList<PcEvidence>> DiscoverAsync(
        string evidenceRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(evidenceRootPath))
            throw new DirectoryNotFoundException($"evidence ルートが見つかりません: {evidenceRootPath}");

        // PC名+シリアルの複合キーで集約 (同一PC名でもシリアルが異なれば別個体として扱う)
        var map = new Dictionary<string, PcEvidence>(StringComparer.OrdinalIgnoreCase);

        // ----- pc_information (最も情報が豊富なため最初に走査) -----
        ScanSubDirectories(
            Path.Combine(evidenceRootPath, EvidenceConstants.DirPcInformation),
            map,
            (evidence, dirPath) => evidence.PcInformationDirectoryPath = dirPath);

        // ----- auto_capture -----
        ScanSubDirectories(
            Path.Combine(evidenceRootPath, EvidenceConstants.DirAutoCapture),
            map,
            (evidence, dirPath) => evidence.AutoCaptureDirectoryPath = dirPath);

        // ----- bitlocker -----
        ScanSubDirectories(
            Path.Combine(evidenceRootPath, EvidenceConstants.DirBitLocker),
            map,
            (evidence, dirPath) => evidence.BitLockerDirectoryPath = dirPath);

        // ----- checklist (ファイル単位) -----
        ScanFiles(
            Path.Combine(evidenceRootPath, EvidenceConstants.DirChecklist),
            EvidenceConstants.PrefixChecklist,
            map,
            (evidence, filePath) => evidence.ChecklistFilePath = filePath);

        // ----- export_history (ファイル単位) -----
        ScanFiles(
            Path.Combine(evidenceRootPath, EvidenceConstants.DirExportHistory),
            EvidenceConstants.PrefixExportHistory,
            map,
            (evidence, filePath) => evidence.ExportHistoryFilePath = filePath);

        IReadOnlyList<PcEvidence> result = map.Values
            .OrderBy(e => e.PcName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// サブディレクトリ名からPC名・シリアル・収集日を抽出し、PcEvidence を取得または新規作成して
    /// パス情報をセットする。(auto_capture, bitlocker, pc_information 用)
    /// 名前順ソートにより、同日再キッティング時は最新タイムスタンプが後勝ちで上書きされる。
    /// </summary>
    private static void ScanSubDirectories(
        string categoryPath,
        Dictionary<string, PcEvidence> map,
        Action<PcEvidence, string> assignPath)
    {
        if (!Directory.Exists(categoryPath))
            return;

        // 名前順ソートで同日再キッティング時に最新タイムスタンプが後勝ち
        var dirs = Directory.EnumerateDirectories(categoryPath)
            .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

        foreach (var dirPath in dirs)
        {
            var dirName = Path.GetFileName(dirPath);
            if (!EvidencePathParser.TryParse(dirName, out var pcName, out var serial, out var date))
                continue;

            var evidence = GetOrCreate(map, pcName, serial, date);
            assignPath(evidence, dirPath);
        }
    }

    /// <summary>
    /// ファイル名からPC名・シリアル・収集日を抽出し、PcEvidence を取得または新規作成して
    /// パス情報をセットする。(checklist, export_history 用)
    /// 名前順ソートにより、同日再キッティング時は最新タイムスタンプが後勝ちで上書きされる。
    /// </summary>
    private static void ScanFiles(
        string categoryPath,
        string filePrefix,
        Dictionary<string, PcEvidence> map,
        Action<PcEvidence, string> assignPath)
    {
        if (!Directory.Exists(categoryPath))
            return;

        // 名前順ソートで同日再キッティング時に最新タイムスタンプが後勝ち
        var files = Directory.EnumerateFiles(categoryPath)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (!EvidencePathParser.TryParseWithPrefix(fileName, filePrefix, out var pcName, out var serial, out var date))
                continue;

            var evidence = GetOrCreate(map, pcName, serial, date);
            assignPath(evidence, filePath);
        }
    }

    /// <summary>
    /// PC名+シリアル+収集日の複合キーで辞書から既存の PcEvidence を取得するか、新規作成して登録する。
    /// </summary>
    private static PcEvidence GetOrCreate(
        Dictionary<string, PcEvidence> map, string pcName, string serial, string collectionDate)
    {
        var key = $"{pcName}\t{serial}\t{collectionDate}";

        if (map.TryGetValue(key, out var existing))
            return existing;

        var evidence = new PcEvidence
        {
            PcName = pcName,
            SerialNumber = serial,
            CollectionDate = collectionDate,
        };
        map[key] = evidence;
        return evidence;
    }
}
