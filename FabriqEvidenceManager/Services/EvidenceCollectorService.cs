using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// 納品用エビデンスディレクトリの自動整理を行う。
/// PC名のみのフォルダを新規作成し、対象PCの各種エビデンスファイルを収集・配置する。
/// 元データには一切の変更を加えず、コピーのみ行う。
///
/// 出力構造:
///   {outputRoot}/
///     {pcName}_{serial}_{collectionDate}/   ← シリアル不明時は {pcName}_{collectionDate}
///       pc_information/    ← ディレクトリ丸ごとコピー
///       auto_capture/      ← ディレクトリ丸ごとコピー
///       bitlocker/         ← ディレクトリ丸ごとコピー
///       checklist.html     ← ファイル単体コピー
///       export_history.csv ← ファイル単体コピー
/// </summary>
public sealed class EvidenceCollectorService : IEvidenceCollectorService
{
    /// <inheritdoc />
    public string CreateDeliveryDirectory(string parentDirectoryPath)
    {
        var deliveryDirName = $"{DateTime.Now:yyyy_MM_dd_HHmmss}_{EvidenceConstants.DeliveryDirSuffix}";
        var deliveryPath = Path.Combine(parentDirectoryPath, deliveryDirName);
        Directory.CreateDirectory(deliveryPath);
        return deliveryPath;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> CollectAsync(
        IReadOnlyList<PcEvidence> pcEvidences,
        string outputDirectoryPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectoryPath);

        var errors = new List<string>();

        // 今回の出力で使用済みのフォルダ名 (同名 PC のファイルが同じフォルダに混ざるのを防ぐ)
        var usedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            for (var i = 0; i < pcEvidences.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pc = pcEvidences[i];
                var folderName = GetPcFolderName(pc, usedFolderNames);
                var pcDir = Path.Combine(outputDirectoryPath, folderName);
                Directory.CreateDirectory(pcDir);

                // pc_information (ディレクトリ全体)
                if (pc.PcInformationDirectoryPath is not null && Directory.Exists(pc.PcInformationDirectoryPath))
                    CopyDirectory(pc.PcInformationDirectoryPath, Path.Combine(pcDir, EvidenceConstants.DirPcInformation), errors, cancellationToken);

                // auto_capture (ディレクトリ全体)
                if (pc.AutoCaptureDirectoryPath is not null && Directory.Exists(pc.AutoCaptureDirectoryPath))
                    CopyDirectory(pc.AutoCaptureDirectoryPath, Path.Combine(pcDir, EvidenceConstants.DirAutoCapture), errors, cancellationToken);

                // bitlocker (ディレクトリ全体)
                if (pc.BitLockerDirectoryPath is not null && Directory.Exists(pc.BitLockerDirectoryPath))
                    CopyDirectory(pc.BitLockerDirectoryPath, Path.Combine(pcDir, EvidenceConstants.DirBitLocker), errors, cancellationToken);

                // checklist (HTMLファイル単体 → 拡張子を保持してコピー)
                if (pc.ChecklistFilePath is not null && File.Exists(pc.ChecklistFilePath))
                {
                    try
                    {
                        var ext = Path.GetExtension(pc.ChecklistFilePath);
                        File.Copy(pc.ChecklistFilePath, Path.Combine(pcDir, $"{EvidenceConstants.DirChecklist}{ext}"), overwrite: true);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        errors.Add($"{pc.ChecklistFilePath}: {ex.Message}");
                    }
                }

                // export_history (CSVファイル単体 → 拡張子を保持してコピー)
                if (pc.ExportHistoryFilePath is not null && File.Exists(pc.ExportHistoryFilePath))
                {
                    try
                    {
                        var ext = Path.GetExtension(pc.ExportHistoryFilePath);
                        File.Copy(pc.ExportHistoryFilePath, Path.Combine(pcDir, $"{EvidenceConstants.DirExportHistory}{ext}"), overwrite: true);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        errors.Add($"{pc.ExportHistoryFilePath}: {ex.Message}");
                    }
                }

                // SHA-256 マニフェスト生成
                GenerateManifest(pcDir, errors, cancellationToken);

                // 進捗報告 (0〜100)
                var percent = (int)((i + 1) / (double)pcEvidences.Count * 100);
                progress?.Report(percent);
            }
        }, cancellationToken);

        return errors;
    }

    /// <summary>
    /// PC 単位の出力フォルダ名 "{PC名}_{シリアル}_{収集日時}" を返す (空の要素は省く)。
    /// シリアル不明でも収集日時で区別し、それでも衝突する場合は "_2", "_3" を付ける。
    /// (以前はシリアル不明時に "{PC名}" のみとなり、同名 PC が同じフォルダに上書き混在していた)
    /// </summary>
    private static string GetPcFolderName(PcEvidence pc, HashSet<string> usedFolderNames)
    {
        var baseName = string.Join("_",
            new[] { pc.PcName, pc.SerialNumber, pc.CollectionDate }
                .Where(s => !string.IsNullOrEmpty(s)));

        if (usedFolderNames.Add(baseName)) return baseName;

        for (var counter = 2; ; counter++)
        {
            var name = $"{baseName}_{counter}";
            if (usedFolderNames.Add(name)) return name;
        }
    }

    private const string ManifestFileName = "manifest_sha256.txt";

    /// <summary>
    /// 指定ディレクトリ内の全ファイルの SHA-256 ハッシュ値を計算し、
    /// manifest_sha256.txt として保存する。manifest 自身は除外する。
    /// </summary>
    private static void GenerateManifest(string directoryPath, List<string> errors, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(directoryPath, ManifestFileName);
        var sb = new StringBuilder();

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // manifest 自身は除外
            if (string.Equals(Path.GetFileName(filePath), ManifestFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(directoryPath, filePath);

            try
            {
                using var stream = File.OpenRead(filePath);
                var hashBytes = SHA256.HashData(stream);
                var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
                sb.AppendLine($"{hashHex}  {relativePath}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                sb.AppendLine($"[ERROR: {ex.Message}]  {relativePath}");
                errors.Add($"{filePath}: マニフェスト生成失敗 - {ex.Message}");
            }
        }

        try
        {
            File.WriteAllText(manifestPath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add($"{manifestPath}: マニフェスト書き込み失敗 - {ex.Message}");
        }
    }

    /// <summary>
    /// ディレクトリを再帰的にコピーする。
    /// サブフォルダ構造を維持しつつ、全ファイルをコピー先に複製する。
    /// 個別ファイルのコピー失敗はスキップし、エラーリストに追記する。
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir, List<string> errors, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);

        // ファイルをコピー
        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fileName = Path.GetFileName(filePath);
                File.Copy(filePath, Path.Combine(destDir, fileName), overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{filePath}: {ex.Message}");
            }
        }

        // サブディレクトリを再帰コピー
        foreach (var subDir in Directory.EnumerateDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subDirName = Path.GetFileName(subDir);
            CopyDirectory(subDir, Path.Combine(destDir, subDirName), errors, cancellationToken);
        }
    }
}
