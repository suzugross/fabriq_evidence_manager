using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
///     {pcName}_{serial}/
///       pc_information/    ← ディレクトリ丸ごとコピー
///       auto_capture/      ← ディレクトリ丸ごとコピー
///       bitlocker/         ← ディレクトリ丸ごとコピー
///       checklist.html     ← ファイル単体コピー
///       export_history.csv ← ファイル単体コピー
/// </summary>
public sealed class EvidenceCollectorService : IEvidenceCollectorService
{
    public async Task CollectAsync(
        IReadOnlyList<PcEvidence> pcEvidences,
        string outputDirectoryPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectoryPath);

        for (var i = 0; i < pcEvidences.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pc = pcEvidences[i];
            var folderName = string.IsNullOrEmpty(pc.SerialNumber)
                ? pc.PcName
                : $"{pc.PcName}_{pc.SerialNumber}";
            var pcDir = Path.Combine(outputDirectoryPath, folderName);
            Directory.CreateDirectory(pcDir);

            // pc_information (ディレクトリ全体)
            if (pc.PcInformationDirectoryPath is not null && Directory.Exists(pc.PcInformationDirectoryPath))
                CopyDirectory(pc.PcInformationDirectoryPath, Path.Combine(pcDir, "pc_information"), cancellationToken);

            // auto_capture (ディレクトリ全体)
            if (pc.AutoCaptureDirectoryPath is not null && Directory.Exists(pc.AutoCaptureDirectoryPath))
                CopyDirectory(pc.AutoCaptureDirectoryPath, Path.Combine(pcDir, "auto_capture"), cancellationToken);

            // bitlocker (ディレクトリ全体)
            if (pc.BitLockerDirectoryPath is not null && Directory.Exists(pc.BitLockerDirectoryPath))
                CopyDirectory(pc.BitLockerDirectoryPath, Path.Combine(pcDir, "bitlocker"), cancellationToken);

            // checklist (HTMLファイル単体 → 拡張子を保持してコピー)
            if (pc.ChecklistFilePath is not null && File.Exists(pc.ChecklistFilePath))
            {
                var ext = Path.GetExtension(pc.ChecklistFilePath);
                File.Copy(pc.ChecklistFilePath, Path.Combine(pcDir, $"checklist{ext}"), overwrite: true);
            }

            // export_history (CSVファイル単体 → 拡張子を保持してコピー)
            if (pc.ExportHistoryFilePath is not null && File.Exists(pc.ExportHistoryFilePath))
            {
                var ext = Path.GetExtension(pc.ExportHistoryFilePath);
                File.Copy(pc.ExportHistoryFilePath, Path.Combine(pcDir, $"export_history{ext}"), overwrite: true);
            }

            // 進捗報告 (0〜100)
            var percent = (int)((i + 1) / (double)pcEvidences.Count * 100);
            progress?.Report(percent);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// ディレクトリを再帰的にコピーする。
    /// サブフォルダ構造を維持しつつ、全ファイルをコピー先に複製する。
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);

        // ファイルをコピー
        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(filePath);
            File.Copy(filePath, Path.Combine(destDir, fileName), overwrite: true);
        }

        // サブディレクトリを再帰コピー
        foreach (var subDir in Directory.EnumerateDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subDirName = Path.GetFileName(subDir);
            CopyDirectory(subDir, Path.Combine(destDir, subDirName), cancellationToken);
        }
    }
}
