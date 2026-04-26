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
/// 各 PC の pc_information/manifest.json を <see cref="IManifestReaderService"/> で読み込み、
/// <see cref="PcEvidence.Manifest"/> または <see cref="PcEvidence.ManifestError"/> に格納する。
/// manifest 不在 PC は ManifestError 経由で UI に表示され、本サービスは load を中断しない。
/// </summary>
public sealed class NestedEvidenceDiscoveryService : IEvidenceDiscoveryService
{
    private readonly IManifestReaderService _manifestReader;

    public NestedEvidenceDiscoveryService(IManifestReaderService manifestReader)
    {
        _manifestReader = manifestReader;
    }

    public Task<IReadOnlyList<PcEvidence>> DiscoverAsync(
        string evidenceRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(evidenceRootPath))
            throw new DirectoryNotFoundException($"evidence ルートが見つかりません: {evidenceRootPath}");

        var mergedMap = new Dictionary<string, PcEvidence>(StringComparer.OrdinalIgnoreCase);

        var pcFolders = Directory.EnumerateDirectories(evidenceRootPath)
            .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

        foreach (var pcFolder in pcFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folderName = Path.GetFileName(pcFolder);
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
    /// フォルダを処理する。親フォルダ名から PC 名・SN・日付をパースし、
    /// evidence/ 配下のカテゴリフォルダを直接割り当てたうえで manifest.json を読み込む。
    /// </summary>
    private void ProcessFolder(
        string folderName,
        string evidencePath,
        Dictionary<string, PcEvidence> mergedMap)
    {
        if (!EvidencePathParser.TryParseNewFormat(folderName, out var pcName, out var serial, out var date))
            return;

        var key = $"{pcName}\t{serial}\t{date}";
        var pc = GetOrMerge(mergedMap, key, pcName, serial, date);

        var pcInfoPath = Path.Combine(evidencePath, EvidenceConstants.DirPcInformation);
        if (Directory.Exists(pcInfoPath))
        {
            pc.PcInformationDirectoryPath = pcInfoPath;
            ReadManifest(pc, pcInfoPath);
        }

        var autoCapturePath = Path.Combine(evidencePath, EvidenceConstants.DirAutoCapture);
        if (Directory.Exists(autoCapturePath))
            pc.AutoCaptureDirectoryPath = autoCapturePath;

        var bitlockerPath = Path.Combine(evidencePath, EvidenceConstants.DirBitLocker);
        if (Directory.Exists(bitlockerPath))
            pc.BitLockerDirectoryPath = bitlockerPath;

        var checklistDir = Path.Combine(evidencePath, EvidenceConstants.DirChecklist);
        if (Directory.Exists(checklistDir))
        {
            var checklistFile = Directory.EnumerateFiles(checklistDir)
                .Where(f => Path.GetFileName(f).StartsWith(EvidenceConstants.PrefixChecklist, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .LastOrDefault();
            if (checklistFile is not null)
                pc.ChecklistFilePath = checklistFile;
        }

        var historyDir = Path.Combine(evidencePath, EvidenceConstants.DirExportHistory);
        if (Directory.Exists(historyDir))
        {
            var historyFile = Directory.EnumerateFiles(historyDir)
                .Where(f => Path.GetFileName(f).StartsWith(EvidenceConstants.PrefixExportHistory, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .LastOrDefault();
            if (historyFile is not null)
                pc.ExportHistoryFilePath = historyFile;
        }
    }

    /// <summary>
    /// manifest.json を読み込み、結果またはエラーメッセージを <see cref="PcEvidence"/> に格納する。
    /// </summary>
    private void ReadManifest(PcEvidence pc, string pcInformationDirectoryPath)
    {
        try
        {
            pc.Manifest = _manifestReader.Read(pcInformationDirectoryPath);
            pc.ManifestError = null;
        }
        catch (ManifestNotFoundException ex)
        {
            pc.ManifestError = $"manifest.json 不在: {ex.Path}";
        }
        catch (UnsupportedManifestSchemaException ex)
        {
            pc.ManifestError = ex.Message;
        }
        catch (ManifestParseException ex)
        {
            pc.ManifestError = $"manifest 解析失敗: {ex.Message}";
        }
    }

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
