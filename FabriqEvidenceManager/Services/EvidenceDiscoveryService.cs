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
    // evidence 直下のサブディレクトリ名 (固定)
    private const string DirAutoCapture = "auto_capture";
    private const string DirBitLocker = "bitlocker";
    private const string DirPcInformation = "pc_information";
    private const string DirChecklist = "checklist";
    private const string DirExportHistory = "export_history";

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
            Path.Combine(evidenceRootPath, DirPcInformation),
            map,
            (evidence, dirPath) => evidence.PcInformationDirectoryPath = dirPath);

        // ----- auto_capture -----
        ScanSubDirectories(
            Path.Combine(evidenceRootPath, DirAutoCapture),
            map,
            (evidence, dirPath) => evidence.AutoCaptureDirectoryPath = dirPath);

        // ----- bitlocker -----
        ScanSubDirectories(
            Path.Combine(evidenceRootPath, DirBitLocker),
            map,
            (evidence, dirPath) => evidence.BitLockerDirectoryPath = dirPath);

        // ----- checklist (ファイル単位) -----
        ScanFiles(
            Path.Combine(evidenceRootPath, DirChecklist),
            "checklist_",
            map,
            (evidence, filePath) => evidence.ChecklistFilePath = filePath);

        // ----- export_history (ファイル単位) -----
        ScanFiles(
            Path.Combine(evidenceRootPath, DirExportHistory),
            "history_export_",
            map,
            (evidence, filePath) => evidence.ExportHistoryFilePath = filePath);

        IReadOnlyList<PcEvidence> result = map.Values
            .OrderBy(e => e.PcName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// サブディレクトリ名からPC名・シリアルを抽出し、PcEvidence を取得または新規作成して
    /// パス情報をセットする。(auto_capture, bitlocker, pc_information 用)
    /// </summary>
    private static void ScanSubDirectories(
        string categoryPath,
        Dictionary<string, PcEvidence> map,
        Action<PcEvidence, string> assignPath)
    {
        if (!Directory.Exists(categoryPath))
            return;

        foreach (var dirPath in Directory.EnumerateDirectories(categoryPath))
        {
            var dirName = Path.GetFileName(dirPath);
            if (!EvidencePathParser.TryParse(dirName, out var pcName, out var serial))
                continue;

            var evidence = GetOrCreate(map, pcName, serial);
            assignPath(evidence, dirPath);
        }
    }

    /// <summary>
    /// ファイル名からPC名・シリアルを抽出し、PcEvidence を取得または新規作成して
    /// パス情報をセットする。(checklist, export_history 用)
    /// </summary>
    private static void ScanFiles(
        string categoryPath,
        string filePrefix,
        Dictionary<string, PcEvidence> map,
        Action<PcEvidence, string> assignPath)
    {
        if (!Directory.Exists(categoryPath))
            return;

        foreach (var filePath in Directory.EnumerateFiles(categoryPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (!EvidencePathParser.TryParseWithPrefix(fileName, filePrefix, out var pcName, out var serial))
                continue;

            var evidence = GetOrCreate(map, pcName, serial);
            assignPath(evidence, filePath);
        }
    }

    /// <summary>
    /// PC名+シリアルの複合キーで辞書から既存の PcEvidence を取得するか、新規作成して登録する。
    /// </summary>
    private static PcEvidence GetOrCreate(
        Dictionary<string, PcEvidence> map, string pcName, string serial)
    {
        var key = $"{pcName}\t{serial}";

        if (map.TryGetValue(key, out var existing))
            return existing;

        var evidence = new PcEvidence
        {
            PcName = pcName,
            SerialNumber = serial,
            CollectionDate = string.Empty,
        };
        map[key] = evidence;
        return evidence;
    }
}
