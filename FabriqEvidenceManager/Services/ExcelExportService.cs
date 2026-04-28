using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;

namespace FabriqEvidenceManager.Services;

/// <summary>
/// PC情報一覧表をExcelファイル (.xlsx) として一括エクスポートする。
/// ClosedXML を使用。
///
/// シート構成:
///   1. PC情報一覧 — 基本情報 + NIC + BitLocker + 突合判定
///   2. 突合詳細   — hostlist との突合結果 (Expected vs Actual)
///   3. チェックリスト — チェックリスト HTML の解析結果
///   4. 実行履歴   — export_history CSV の内容
/// </summary>
public sealed class ExcelExportService : IExcelExportService
{
    // 固定列数 (計画PC名, 実PC名, シリアル番号, SNソース, 収集日, 突合判定)
    private const int FixedColumnCount = 6;

    // M14: バッテリ列数 (Health % / Design / FullCharge)
    private const int BatterySubColumnCount = 3;

    // NIC1列あたりのサブ列数 (MAC, アダプタ, 接続名)
    private const int NicSubColumnCount = 3;

    // ドライブ1つあたりのサブ列数 (キーID, 回復キー)
    private const int DriveSubColumnCount = 2;

    // ヘッダー背景色
    private static readonly XLColor BaseHeaderBg = XLColor.FromHtml("#4472C4");       // 青: 基本情報
    private static readonly XLColor NicHeaderBg = XLColor.FromHtml("#548235");        // 緑: NIC
    private static readonly XLColor KeyHeaderBg = XLColor.FromHtml("#BF8F00");        // 黄: 回復キー
    private static readonly XLColor VerifyHeaderBg = XLColor.FromHtml("#7030A0");     // 紫: 突合
    private static readonly XLColor ChecklistHeaderBg = XLColor.FromHtml("#C55A11");  // オレンジ: チェックリスト
    private static readonly XLColor HistoryHeaderBg = XLColor.FromHtml("#538DD5");    // 水色: 実行履歴
    private static readonly XLColor LicenseHeaderBg = XLColor.FromHtml("#7B1FA2");    // 紫: ライセンス監査
    private static readonly XLColor HeaderFontColor = XLColor.White;

    // ステータス色
    private static readonly XLColor MatchBg = XLColor.FromHtml("#E2EFDA");       // 薄緑
    private static readonly XLColor MismatchBg = XLColor.FromHtml("#FCE4EC");    // 薄赤
    private static readonly XLColor WarningBg = XLColor.FromHtml("#FFF8E1");     // 薄黄: Partial / 要確認 (M11)
    private static readonly XLColor InfoBg = XLColor.FromHtml("#F5F5F5");        // 薄グレー: 情報行 (Office 未インストール等)
    private static readonly XLColor MismatchFont = XLColor.FromHtml("#C62828");  // 赤文字
    private static readonly XLColor MatchFont = XLColor.FromHtml("#2E7D32");     // 緑文字
    private static readonly XLColor CautionBg = XLColor.FromHtml("#FFF3E0");     // 薄オレンジ: フォールバック取得 (要確認)
    private static readonly XLColor CautionFont = XLColor.FromHtml("#E65100");   // オレンジ文字

    /// <summary>未知 section シート用の本アプリのバージョン (manifest 経由の前方互換情報)</summary>
    private static readonly string ManagerVersion =
        typeof(ExcelExportService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Excel セル 1 個あたりの最大文字数 (ClosedXML / .xlsx 仕様)</summary>
    private const int ExcelCellMaxChars = 32_767;

    /// <summary>
    /// 32,767 文字を超えるテキストを安全にセルに収まるよう切り詰める。
    /// 切り詰め時は末尾に [TRUNCATED] マーカーを付与して欠損を明示する。
    /// メモ / 未知 section raw 等、長文の可能性があるセル値で使用。
    /// </summary>
    private static string TruncateForCell(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length <= ExcelCellMaxChars) return text;
        const string marker = "\n... [TRUNCATED]";
        return text[..(ExcelCellMaxChars - marker.Length)] + marker;
    }

    public Task ExportAsync(
        IReadOnlyList<PcEvidence> pcEvidences,
        string outputFilePath,
        ExcelExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 動的列の幅を決定: 全PCの最大NIC数と全ドライブレターの集合
        var maxNicCount = pcEvidences.Count > 0
            ? pcEvidences.Max(pc => pc.MacAddresses.Count)
            : 0;

        var allDriveLetters = pcEvidences
            .SelectMany(pc => pc.BitLockerKeys)
            .Select(k => k.DriveLetter)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var totalColumns = FixedColumnCount
                           + maxNicCount * NicSubColumnCount
                           + allDriveLetters.Count * DriveSubColumnCount
                           + BatterySubColumnCount
                           + 1;   // 管理者メモ列 (末尾)

        // ===== STEP 1: PC -> 詳細ファイル相対パスを pre-compute =====
        // 詳細データを持つ PC のみエントリ化 (ファイル衝突回避を伴う)
        var mainDir = Path.GetDirectoryName(outputFilePath) ?? string.Empty;
        var detailDir = Path.Combine(mainDir, EvidenceConstants.PcDetailsSubdirName);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var detailRelPaths = new Dictionary<PcEvidence, string>();
        foreach (var pc in pcEvidences)
        {
            if (!HasAnyDetailData(pc, options)) continue;
            var fileName = GetPcDetailFileName(pc, usedNames);
            detailRelPaths[pc] = $"{EvidenceConstants.PcDetailsSubdirName}/{fileName}";
        }

        // ===== STEP 2: 詳細ファイルを出力 =====
        if (detailRelPaths.Count > 0)
        {
            Directory.CreateDirectory(detailDir);
            foreach (var (pc, relPath) in detailRelPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var detailPath = Path.Combine(mainDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                ExportPcDetailWorkbook(pc, detailPath, options, cancellationToken);
            }
        }

        // ===== STEP 3: main ファイル (1 行/PC のサマリシートのみ) =====
        using var workbook = new XLWorkbook();

        // ---- Sheet 1: PC情報一覧 (PC 名は詳細ファイルへハイパーリンク) ----
        var ws = workbook.Worksheets.Add(EvidenceConstants.ExcelSheetName);

        // M6: 案件メタデータブロック (1〜6 行目)
        var headerRow = 1;
        if (options?.DeliveryMetadata is not null)
        {
            WriteDeliveryMetadataBlock(ws, options.DeliveryMetadata, pcEvidences, options);
            headerRow = 7;
        }

        WriteMainHeader(ws, headerRow, maxNicCount, allDriveLetters);

        for (var i = 0; i < pcEvidences.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            detailRelPaths.TryGetValue(pcEvidences[i], out var relPath);
            WriteMainDataRow(ws, headerRow + 1 + i, pcEvidences[i], maxNicCount, allDriveLetters, options, relPath);
        }

        ApplyStyles(ws, pcEvidences.Count, totalColumns, headerRow);

        // ---- Sheet 2: ドメイン状態 (1 行/PC) ----
        if (pcEvidences.Any(pc => pc.DomainStatus is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteDomainStatusSheet(workbook, pcEvidences);
        }

        // ---- Sheet 3: 電源設定 (1 行/PC) ----
        if (pcEvidences.Any(pc => pc.PowerSettings is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WritePowerSettingsSheet(workbook, pcEvidences);
        }

        // ---- Sheet 4: Windows Defender (1 行/PC) ----
        if (pcEvidences.Any(pc => pc.DefenderStatus is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteDefenderStatusSheet(workbook, pcEvidences);
        }

        // ---- Sheet 5: Windowsライセンス (典型的に 1 行/PC、フリート license posture 一覧用途) ----
        if (pcEvidences.Any(pc => pc.WindowsLicense is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteWindowsLicenseSheet(workbook, pcEvidences);
        }

        // ---- Sheet 6: Officeライセンス ----
        if (pcEvidences.Any(pc => pc.OfficeLicense is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteOfficeLicenseSheet(workbook, pcEvidences);
        }

        // ---- Sheet 7: セキュリティベースライン (1 行/PC) ----
        if (pcEvidences.Any(pc => pc.SecurityBaseline is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSecurityBaselineSheet(workbook, pcEvidences);
        }

        // 証明書 (§25) は N 行/PC のため main ではなく pc_details/ 側に出力する
        // (M13 当初は main にフラット集約していたが、フリート規模で行数が膨大になるため
        // 他の N 行データと同じ流派に揃えた)。

        workbook.SaveAs(outputFilePath);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 1 PC 分の詳細 Excel ファイルを出力する。
    /// 「1 PC = N 行になりうる」全シート (突合詳細/チェックリスト/実行履歴/ベースライン/
    /// ユーザー・グループ/FWプロファイル/FWルール/オプション機能/ユーザープロファイル/
    /// ディスク・パーティション/WiFi/復元ポイント/Windows Update/未知 section) を含む。
    /// 既存のシート出力メソッドを単一要素リストで再利用 (DRY)。
    /// </summary>
    private static void ExportPcDetailWorkbook(
        PcEvidence pc, string detailFilePath,
        ExcelExportOptions? options, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var single = new[] { pc };

        if (options?.VerificationReports?.ContainsKey(pc) == true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteVerificationSheet(workbook, single, options.VerificationReports);
        }
        if (options?.ChecklistResults?.ContainsKey(pc) == true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteChecklistSheet(workbook, single, options.ChecklistResults);
        }
        if (options?.ExportHistories?.ContainsKey(pc) == true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteHistorySheet(workbook, single, options.ExportHistories);
        }
        if (options?.BaselineReports?.TryGetValue(pc, out var br) == true)
        {
            // 5 カテゴリ × 該当データ有り = それぞれ独立シートで出力
            // (Phase 1.5: ベースライン全カテゴリ Excel 出力)
            if (br.Items.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteBaselineExecutionSheet(workbook, single, options.BaselineReports);
            }
            if (br.SystemInfoComparison is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteBaselineSystemInfoSheet(workbook, single, options.BaselineReports);
            }
            if (br.ChecklistComparison is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteBaselineChecklistSheet(workbook, single, options.BaselineReports);
            }
            if (br.InstalledAppsComparison is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteBaselineAppsSheet(workbook, single, options.BaselineReports);
            }
            if (br.LicenseComparison is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteBaselineLicenseSheet(workbook, single, options.BaselineReports);
            }
            if (br.DomainStatusComparison is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteBaselineDomainStatusSheet(workbook, single, options.BaselineReports);
            }
        }
        if (pc.LocalUsers.Count > 0 || pc.LocalGroups.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteUserGroupSheet(workbook, single);
        }
        if (pc.FirewallProfiles.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteFirewallProfileSheet(workbook, single);
        }
        if (pc.FirewallRules.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteFirewallRuleSheet(workbook, single);
        }
        if (pc.OptionalFeatures.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteOptionalFeatureSheet(workbook, single);
        }
        if (pc.UserProfiles.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteUserProfileSheet(workbook, single);
        }
        if (pc.Disks.Count > 0 || pc.Partitions.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteDiskPartitionSheet(workbook, single);
        }
        if (pc.WiFiProfiles.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteWiFiProfileSheet(workbook, single);
        }
        if (pc.RestorePoints.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteRestorePointSheet(workbook, single);
        }
        if (pc.WindowsUpdates.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteWindowsUpdateSheet(workbook, single);
        }
        if (pc.Certificates.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCertificatesSheet(workbook, single);
        }
        if (pc.UnknownSections.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteUnknownSectionSheets(workbook, single);
        }

        workbook.SaveAs(detailFilePath);
    }

    /// <summary>PC 詳細ファイル名を生成。衝突時は serial 先頭 6 文字、それでも衝突なら連番。</summary>
    private static string GetPcDetailFileName(PcEvidence pc, HashSet<string> usedNames)
    {
        var pcSeg = SanitizeFileNameSegment(pc.PcName);
        var dateSeg = SanitizeFileNameSegment(pc.CollectionDate);
        var baseName = $"{pcSeg}_{dateSeg}";
        var name = $"{baseName}.xlsx";

        if (usedNames.Add(name)) return name;

        var serialFrag = string.IsNullOrEmpty(pc.SerialNumber)
            ? "noserial"
            : pc.SerialNumber.Length >= 6 ? pc.SerialNumber[..6] : pc.SerialNumber;
        serialFrag = SanitizeFileNameSegment(serialFrag);
        var serialName = $"{baseName}_{serialFrag}.xlsx";
        if (usedNames.Add(serialName)) return serialName;

        for (var counter = 2; ; counter++)
        {
            var counterName = $"{baseName}_{serialFrag}_{counter}.xlsx";
            if (usedNames.Add(counterName)) return counterName;
        }
    }

    /// <summary>Windows ファイル名禁則文字を除去。</summary>
    private static string SanitizeFileNameSegment(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']', '"', '<', '>', '|' };
        return string.Concat(name.Where(c => !invalid.Contains(c)));
    }

    /// <summary>この PC が詳細ファイル生成対象のデータ (N 行/PC になるシート群) を持つか。</summary>
    private static bool HasAnyDetailData(PcEvidence pc, ExcelExportOptions? options)
    {
        return options?.VerificationReports?.ContainsKey(pc) == true
            || options?.ChecklistResults?.ContainsKey(pc) == true
            || options?.ExportHistories?.ContainsKey(pc) == true
            || options?.BaselineReports?.ContainsKey(pc) == true
            || pc.LocalUsers.Count > 0 || pc.LocalGroups.Count > 0
            || pc.FirewallProfiles.Count > 0
            || pc.FirewallRules.Count > 0
            || pc.OptionalFeatures.Count > 0
            || pc.UserProfiles.Count > 0
            || pc.Disks.Count > 0 || pc.Partitions.Count > 0
            || pc.WiFiProfiles.Count > 0
            || pc.RestorePoints.Count > 0
            || pc.WindowsUpdates.Count > 0
            || pc.Certificates.Count > 0
            || pc.UnknownSections.Count > 0;
    }

    // ================================================================
    // Sheet 1: PC情報一覧
    // ================================================================

    private static void WriteMainHeader(IXLWorksheet ws, int headerRow, int maxNicCount, List<string> driveLetters)
    {
        var col = 1;

        // 固定列 (計画 PC 名と実 PC 名を併記 + SN とその採用ソースを併記)
        SetHeaderCell(ws, headerRow, col++, "計画 PC 名", BaseHeaderBg);
        SetHeaderCell(ws, headerRow, col++, "実 PC 名", BaseHeaderBg);
        SetHeaderCell(ws, headerRow, col++, "シリアル番号", BaseHeaderBg);
        SetHeaderCell(ws, headerRow, col++, "SN ソース", BaseHeaderBg);
        SetHeaderCell(ws, headerRow, col++, "収集日", BaseHeaderBg);
        SetHeaderCell(ws, headerRow, col++, "突合判定", BaseHeaderBg);

        // NIC列 (動的)
        for (var i = 1; i <= maxNicCount; i++)
        {
            SetHeaderCell(ws, headerRow, col++, $"NIC{i}_アダプタ", NicHeaderBg);
            SetHeaderCell(ws, headerRow, col++, $"NIC{i}_接続名", NicHeaderBg);
            SetHeaderCell(ws, headerRow, col++, $"NIC{i}_MAC", NicHeaderBg);
        }

        // BitLocker列 (動的: ドライブレター別)
        foreach (var drive in driveLetters)
        {
            SetHeaderCell(ws, headerRow, col++, $"{drive}ドライブ_キーID", KeyHeaderBg);
            SetHeaderCell(ws, headerRow, col++, $"{drive}ドライブ_回復キー", KeyHeaderBg);
        }

        // バッテリ列 (M14: 全 PC 共通 3 列、デスクトップは N/A)
        SetHeaderCell(ws, headerRow, col++, "バッテリ健全性 (%)", LicenseHeaderBg);
        SetHeaderCell(ws, headerRow, col++, "設計容量 (mWh)", LicenseHeaderBg);
        SetHeaderCell(ws, headerRow, col++, "満充電容量 (mWh)", LicenseHeaderBg);

        // 管理者メモ列 (末尾固定、wrap text)
        SetHeaderCell(ws, headerRow, col++, "管理者メモ", BaseHeaderBg);
    }

    /// <summary>
    /// M6: Sheet 1 冒頭の案件メタデータブロック (6 行)。
    /// 1: 案件名 / 2: 顧客 / 3: 作業者 / 4: 作業日 /
    /// 5: kernelVersion + configVersion / 6: PC台数 + 突合 OK・NG 件数
    /// </summary>
    private static void WriteDeliveryMetadataBlock(
        IXLWorksheet ws,
        ExcelDeliveryMetadata metadata,
        IReadOnlyList<PcEvidence> pcEvidences,
        ExcelExportOptions? options)
    {
        ws.Cell(1, 1).Value = "案件名";
        ws.Cell(1, 2).Value = metadata.ProjectName;

        ws.Cell(2, 1).Value = "顧客";
        ws.Cell(2, 2).Value = metadata.CustomerName;

        ws.Cell(3, 1).Value = "作業者";
        ws.Cell(3, 2).Value = metadata.WorkerName;

        ws.Cell(4, 1).Value = "作業日";
        ws.Cell(4, 2).Value = metadata.WorkDate?.ToString("yyyy/MM/dd") ?? string.Empty;

        // fabriq versions: 最初に manifest が読めた PC の値を採用
        var firstManifest = pcEvidences.FirstOrDefault(pc => pc.Manifest is not null)?.Manifest;
        ws.Cell(5, 1).Value = "fabriqKernelVersion";
        ws.Cell(5, 2).Value = firstManifest?.FabriqKernelVersion ?? "(取得不能)";
        ws.Cell(5, 3).Value = "evidenceConfigVersion";
        ws.Cell(5, 4).Value = firstManifest?.EvidenceConfigVersion ?? "(取得不能)";

        var (ok, ng) = ComputeVerificationCounts(pcEvidences, options);
        ws.Cell(6, 1).Value = "対象 PC 台数";
        ws.Cell(6, 2).Value = pcEvidences.Count;
        ws.Cell(6, 3).Value = "突合 OK";
        ws.Cell(6, 4).Value = ok;
        ws.Cell(6, 5).Value = "突合 NG";
        ws.Cell(6, 6).Value = ng;

        // ラベル列を太字 + 薄背景
        var labelBg = XLColor.FromHtml("#E0E0E0");
        for (var r = 1; r <= 6; r++)
        {
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Fill.BackgroundColor = labelBg;
        }
        // 5/6 行の中段ラベル列も太字 + 薄背景
        ws.Cell(5, 3).Style.Font.Bold = true;
        ws.Cell(5, 3).Style.Fill.BackgroundColor = labelBg;
        ws.Cell(6, 3).Style.Font.Bold = true;
        ws.Cell(6, 3).Style.Fill.BackgroundColor = labelBg;
        ws.Cell(6, 5).Style.Font.Bold = true;
        ws.Cell(6, 5).Style.Fill.BackgroundColor = labelBg;

        // NG > 0 の場合は強調
        if (ng > 0)
        {
            ws.Cell(6, 4).Style.Font.FontColor = MismatchFont;
            ws.Cell(6, 4).Style.Font.Bold = true;
        }
    }

    private static (int Ok, int Ng) ComputeVerificationCounts(
        IReadOnlyList<PcEvidence> pcEvidences,
        ExcelExportOptions? options)
    {
        if (options?.VerificationReports is null) return (0, 0);
        var ok = 0;
        var ng = 0;
        foreach (var pc in pcEvidences)
        {
            if (!options.VerificationReports.TryGetValue(pc, out var rep)) continue;
            if (rep.MismatchCount > 0) ng++;
            else if (rep.AllMatch) ok++;
        }
        return (ok, ng);
    }

    private static void WriteMainDataRow(
        IXLWorksheet ws, int row, PcEvidence pc,
        int maxNicCount, List<string> driveLetters,
        ExcelExportOptions? options,
        string? detailRelativePath)
    {
        var col = 1;

        // 計画 PC 名 (詳細ファイルがあれば hyperlink を張る) + 実 PC 名併記
        var pcNameCell = ws.Cell(row, col++);
        pcNameCell.Value = pc.PcName;
        if (detailRelativePath is not null)
        {
            pcNameCell.SetHyperlink(new XLHyperlink(detailRelativePath));
            pcNameCell.Style.Font.Underline = XLFontUnderlineValues.Single;
            pcNameCell.Style.Font.FontColor = XLColor.FromHtml("#1565C0");
        }
        var actualCell = ws.Cell(row, col++);
        actualCell.Value = pc.ActualComputerNameDisplay;
        if (pc.HasPcNameMismatch)
        {
            actualCell.Style.Fill.BackgroundColor = MismatchBg;
            actualCell.Style.Font.FontColor = MismatchFont;
            actualCell.Style.Font.Bold = true;
        }

        ws.Cell(row, col++).Value = pc.SerialNumber;

        // SN ソース (Primary=BIOS 以外はオレンジ強調 / 取得不能はグレー)
        var sourceCell = ws.Cell(row, col++);
        if (string.IsNullOrEmpty(pc.SerialNumber))
        {
            sourceCell.Value = "(取得不能)";
            sourceCell.Style.Font.FontColor = XLColor.Gray;
        }
        else
        {
            sourceCell.Value = SerialSourceEntry.ShortLabel(pc.SerialNumberSource);
            if (pc.HasSerialFallbackWarning)
            {
                sourceCell.Style.Fill.BackgroundColor = CautionBg;
                sourceCell.Style.Font.FontColor = CautionFont;
                sourceCell.Style.Font.Bold = true;
            }
        }

        ws.Cell(row, col++).Value = pc.CollectionDate;

        // 突合判定列
        var verifyCell = ws.Cell(row, col++);
        if (options?.VerificationReports is not null
            && options.VerificationReports.TryGetValue(pc, out var report))
        {
            if (!report.HostlistFound)
            {
                verifyCell.Value = "対象外";
                verifyCell.Style.Font.FontColor = XLColor.Gray;
            }
            else if (report.MismatchCount == 0)
            {
                verifyCell.Value = "OK";
                verifyCell.Style.Font.FontColor = MatchFont;
                verifyCell.Style.Font.Bold = true;
                verifyCell.Style.Fill.BackgroundColor = MatchBg;
            }
            else
            {
                verifyCell.Value = $"NG ({report.MismatchCount}件)";
                verifyCell.Style.Font.FontColor = MismatchFont;
                verifyCell.Style.Font.Bold = true;
                verifyCell.Style.Fill.BackgroundColor = MismatchBg;
            }
        }
        else
        {
            verifyCell.Value = "未突合";
            verifyCell.Style.Font.FontColor = XLColor.Gray;
        }

        // NIC列 (接続名でソートして全PC間の列順を統一)
        var sortedMacs = pc.MacAddresses.OrderBy(m => m.ConnectionName, StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < maxNicCount; i++)
        {
            if (i < sortedMacs.Count)
            {
                var mac = sortedMacs[i];
                ws.Cell(row, col++).Value = mac.AdapterName;
                ws.Cell(row, col++).Value = mac.ConnectionName;
                ws.Cell(row, col++).Value = mac.PhysicalAddress;
            }
            else
            {
                col += NicSubColumnCount;
            }
        }

        // BitLocker列 (ドライブレター別)
        foreach (var drive in driveLetters)
        {
            var key = pc.BitLockerKeys.FirstOrDefault(k => k.DriveLetter == drive);
            if (key is not null)
            {
                ws.Cell(row, col++).Value = key.KeyId;
                ws.Cell(row, col++).Value = key.RecoveryPassword;
            }
            else
            {
                col += DriveSubColumnCount;
            }
        }

        // バッテリ列 (M14: 健全性 % / 設計 mWh / 満充電 mWh)
        WriteBatteryCells(ws, row, col, pc.BatteryReport);
        col += BatterySubColumnCount;

        // 管理者メモ (末尾、wrap text、32,767 文字超は truncate)
        var memoCell = ws.Cell(row, col++);
        memoCell.Value = TruncateForCell(pc.Memo?.Text);
        memoCell.Style.Alignment.WrapText = true;
    }

    /// <summary>
    /// PC情報一覧シートのバッテリ 3 列を書き込む (M14)。
    /// 受入検査基準着色: &gt;= 95% 緑 / 90-95% 黄 / &lt; 90% 赤 / Battery 非搭載 グレー N/A。
    /// </summary>
    private static void WriteBatteryCells(IXLWorksheet ws, int row, int colStart, BatteryReportData? battery)
    {
        var healthCell = ws.Cell(row, colStart);
        var designCell = ws.Cell(row, colStart + 1);
        var fullCell = ws.Cell(row, colStart + 2);

        if (battery is null)
        {
            // デスクトップ等で §26 が Skipped → 3 列まとめて N/A
            WriteNa(healthCell);
            WriteNa(designCell);
            WriteNa(fullCell);
            return;
        }

        if (battery.HealthPercent.HasValue)
        {
            healthCell.Value = battery.HealthPercent.Value;
            healthCell.Style.NumberFormat.Format = "0.0";
            var pct = battery.HealthPercent.Value;
            var level = pct >= 95 ? AuditLevel.Good
                : pct >= 90 ? AuditLevel.Warning
                : AuditLevel.Bad;
            ApplyCellAuditColor(healthCell, level);
        }
        else
        {
            WriteNa(healthCell);
        }

        if (battery.DesignCapacityMwh.HasValue)
            designCell.Value = battery.DesignCapacityMwh.Value;
        else
            WriteNa(designCell);

        if (battery.FullChargeCapacityMwh.HasValue)
            fullCell.Value = battery.FullChargeCapacityMwh.Value;
        else
            WriteNa(fullCell);
    }

    // ================================================================
    // Sheet 2: 突合詳細
    // ================================================================

    private static void WriteVerificationSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, VerificationReport> reports)
    {
        var ws = workbook.Worksheets.Add("突合詳細");

        // ヘッダー
        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 2, "チェック項目", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 3, "期待値 (hostlist)", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 4, "実測値 (evidence)", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 5, "判定", VerifyHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc, out var report)) continue;
            if (!report.HostlistFound) continue;

            foreach (var item in report.Items)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = item.ItemName;
                ws.Cell(row, 3).Value = item.Expected;
                ws.Cell(row, 4).Value = item.Actual;

                var statusCell = ws.Cell(row, 5);
                switch (item.Status)
                {
                    case VerificationStatus.Match:
                        statusCell.Value = "OK";
                        statusCell.Style.Font.FontColor = MatchFont;
                        statusCell.Style.Fill.BackgroundColor = MatchBg;
                        break;
                    case VerificationStatus.Mismatch:
                        statusCell.Value = "NG";
                        statusCell.Style.Font.FontColor = MismatchFont;
                        statusCell.Style.Font.Bold = true;
                        statusCell.Style.Fill.BackgroundColor = MismatchBg;
                        // 不一致行全体を薄赤に
                        ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = MismatchBg;
                        break;
                    case VerificationStatus.NoExpected:
                        statusCell.Value = "期待値なし";
                        statusCell.Style.Font.FontColor = XLColor.Gray;
                        break;
                    case VerificationStatus.NoActual:
                        statusCell.Value = "実測値なし";
                        statusCell.Style.Font.FontColor = XLColor.FromHtml("#E65100");
                        statusCell.Style.Font.Bold = true;
                        ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = MismatchBg;
                        break;
                }

                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    // ================================================================
    // Sheet 3: チェックリスト
    // ================================================================

    private static void WriteChecklistSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, ChecklistResult> results)
    {
        var ws = workbook.Worksheets.Add("チェックリスト");

        // ヘッダー (Verified 列は kernel 2.x で新設、Result の右に挿入)
        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 2, "全体判定", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 3, "#", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 4, "モジュール名", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 5, "カテゴリ", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 6, "結果", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 7, "ベリファイ", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 8, "実行時刻", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 9, "メッセージ", ChecklistHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!results.TryGetValue(pc, out var result)) continue;

            if (result.ModuleItems.Count == 0)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = result.OverallStatus;
                ApplyStatusColor(ws.Cell(row, 2), result.OverallStatus);
                row++;
                continue;
            }

            foreach (var module in result.ModuleItems)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = result.OverallStatus;
                ApplyStatusColor(ws.Cell(row, 2), result.OverallStatus);
                ws.Cell(row, 3).Value = module.Order;
                ws.Cell(row, 4).Value = module.ModuleName;
                ws.Cell(row, 5).Value = module.Category;
                var statusCell = ws.Cell(row, 6);
                statusCell.Value = module.Status;
                ApplyStatusColor(statusCell, module.Status);
                var verifiedCell = ws.Cell(row, 7);
                verifiedCell.Value = module.VerifiedStatus;
                ApplyStatusColor(verifiedCell, module.VerifiedStatus);
                ws.Cell(row, 8).Value = module.Time;
                ws.Cell(row, 9).Value = TruncateForCell(module.Message);
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 9);
    }

    // ================================================================
    // Sheet 4: 実行履歴
    // ================================================================

    private static void WriteHistorySheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, IReadOnlyList<ExportHistoryEntry>> histories)
    {
        var ws = workbook.Worksheets.Add("実行履歴");

        // ヘッダー
        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", HistoryHeaderBg);
        SetHeaderCell(ws, 1, 2, "タイムスタンプ", HistoryHeaderBg);
        SetHeaderCell(ws, 1, 3, "モジュール名", HistoryHeaderBg);
        SetHeaderCell(ws, 1, 4, "カテゴリ", HistoryHeaderBg);
        SetHeaderCell(ws, 1, 5, "ステータス", HistoryHeaderBg);
        SetHeaderCell(ws, 1, 6, "メッセージ", HistoryHeaderBg);
        SetHeaderCell(ws, 1, 7, "作業者", HistoryHeaderBg);
        SetHeaderCell(ws, 1, 8, "セッションID", HistoryHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!histories.TryGetValue(pc, out var entries)) continue;

            foreach (var entry in entries)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = entry.Timestamp;
                ws.Cell(row, 3).Value = entry.ModuleName;
                ws.Cell(row, 4).Value = entry.Category;
                var statusCell = ws.Cell(row, 5);
                statusCell.Value = entry.Status;
                ApplyStatusColor(statusCell, entry.Status);
                ws.Cell(row, 6).Value = TruncateForCell(entry.Message);
                ws.Cell(row, 7).Value = entry.Worker;
                ws.Cell(row, 8).Value = entry.SessionID;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 8);
    }

    // ================================================================
    // Sheet 5: ベースライン突合
    // ================================================================

    private static readonly XLColor BaselineHeaderBg = XLColor.FromHtml("#5B5EA6"); // 青紫: ベースライン

    /// <summary>ベースライン突合 — 実行サマリ (旧「ベースライン突合」シートを命名統一)</summary>
    private static void WriteBaselineExecutionSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, BaselineComparisonReport> reports)
    {
        var ws = workbook.Worksheets.Add("ベースライン_実行サマリ");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 2, "モジュール名", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 3, "カテゴリ", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 4, "期待ステータス", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 5, "実測ステータス", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 6, "判定", BaselineHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc, out var report)) continue;

            foreach (var item in report.Items)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = item.ModuleName;
                ws.Cell(row, 3).Value = item.Category;
                ws.Cell(row, 4).Value = item.ExpectedStatus;
                ws.Cell(row, 5).Value = item.ActualStatus;

                var statusCell = ws.Cell(row, 6);
                switch (item.MatchStatus)
                {
                    case BaselineMatchStatus.Match:
                        statusCell.Value = "OK";
                        statusCell.Style.Font.FontColor = MatchFont;
                        statusCell.Style.Fill.BackgroundColor = MatchBg;
                        break;
                    case BaselineMatchStatus.Mismatch:
                        statusCell.Value = "NG";
                        statusCell.Style.Font.FontColor = MismatchFont;
                        statusCell.Style.Font.Bold = true;
                        statusCell.Style.Fill.BackgroundColor = MismatchBg;
                        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = MismatchBg;
                        break;
                    case BaselineMatchStatus.MissingInActual:
                        statusCell.Value = "未実行";
                        statusCell.Style.Font.FontColor = XLColor.FromHtml("#E65100");
                        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = MismatchBg;
                        break;
                    case BaselineMatchStatus.ExtraInActual:
                        statusCell.Value = "追加";
                        statusCell.Style.Font.FontColor = XLColor.Gray;
                        break;
                }

                row++;
            }
        }

        ApplyStyles(ws, row - 2, 6);
    }

    /// <summary>ベースライン突合 — SystemInfo (OS名 / OSバージョン / CPU / メモリ 4 項目)</summary>
    private static void WriteBaselineSystemInfoSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, BaselineComparisonReport> reports)
    {
        var ws = workbook.Worksheets.Add("ベースライン_SystemInfo");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 2, "項目", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 3, "期待値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 4, "実測値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 5, "判定", BaselineHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc, out var report)) continue;
            if (report.SystemInfoComparison is null) continue;

            foreach (var item in report.SystemInfoComparison.Items)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = item.ItemName;
                ws.Cell(row, 3).Value = item.Expected;
                ws.Cell(row, 4).Value = item.Actual;
                ApplyVerificationStatusToCell(ws.Cell(row, 5), item.Status, ws.Range(row, 1, row, 5));
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    /// <summary>ベースライン突合 — チェックリスト (OverallStatus + verify-table 各項目)</summary>
    private static void WriteBaselineChecklistSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, BaselineComparisonReport> reports)
    {
        var ws = workbook.Worksheets.Add("ベースライン_チェックリスト");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 2, "種別", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 3, "項目", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 4, "期待値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 5, "実測値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 6, "判定", BaselineHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc, out var report)) continue;
            if (report.ChecklistComparison is null) continue;
            var cl = report.ChecklistComparison;

            // OverallStatus 行 (1 行)
            ws.Cell(row, 1).Value = pc.PcNameDisplay;
            ws.Cell(row, 2).Value = "全体判定";
            ws.Cell(row, 3).Value = "OverallStatus";
            ws.Cell(row, 4).Value = cl.BaselineOverallStatus;
            ws.Cell(row, 5).Value = cl.ActualOverallStatus;

            var overallCell = ws.Cell(row, 6);
            if (cl.OverallStatusMatch)
            {
                overallCell.Value = "OK";
                overallCell.Style.Font.FontColor = MatchFont;
                overallCell.Style.Fill.BackgroundColor = MatchBg;
            }
            else
            {
                overallCell.Value = "NG";
                overallCell.Style.Font.FontColor = MismatchFont;
                overallCell.Style.Font.Bold = true;
                overallCell.Style.Fill.BackgroundColor = MismatchBg;
                ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = MismatchBg;
            }
            row++;

            // VerifyItems 行 (N 行)
            foreach (var item in cl.VerifyItemComparisons)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = "検証項目";
                ws.Cell(row, 3).Value = item.ItemName;
                ws.Cell(row, 4).Value = item.Expected;
                ws.Cell(row, 5).Value = item.Actual;
                ApplyVerificationStatusToCell(ws.Cell(row, 6), item.Status, ws.Range(row, 1, row, 6));
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 6);
    }

    /// <summary>ベースライン突合 — インストール済みアプリ (PC ごと集計行 + 差分行)</summary>
    private static void WriteBaselineAppsSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, BaselineComparisonReport> reports)
    {
        var ws = workbook.Worksheets.Add("ベースライン_アプリ");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 2, "アプリ名", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 3, "期待バージョン", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 4, "実測バージョン", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 5, "判定", BaselineHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc, out var report)) continue;
            if (report.InstalledAppsComparison is null) continue;
            var apps = report.InstalledAppsComparison;

            // 集計行 (1 PC = 1 行)
            ws.Cell(row, 1).Value = pc.PcNameDisplay;
            ws.Cell(row, 2).Value = "(集計)";
            ws.Cell(row, 3).Value = $"ベースライン {apps.TotalBaselineCount}件";
            ws.Cell(row, 4).Value = $"対象 {apps.TotalActualCount}件";
            var summaryCell = ws.Cell(row, 5);
            summaryCell.Value = $"一致 {apps.MatchCount}件 / 差分 {apps.MismatchCount}件";
            if (apps.MismatchCount == 0)
            {
                summaryCell.Style.Font.FontColor = MatchFont;
                summaryCell.Style.Font.Bold = true;
            }
            else
            {
                summaryCell.Style.Font.FontColor = MismatchFont;
                summaryCell.Style.Font.Bold = true;
            }
            row++;

            // 差分行 (Match 以外のみ)
            foreach (var item in apps.Items)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = item.ItemName;
                ws.Cell(row, 3).Value = item.Expected;
                ws.Cell(row, 4).Value = item.Actual;
                ApplyVerificationStatusToCell(ws.Cell(row, 5), item.Status, ws.Range(row, 1, row, 5));
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    /// <summary>ベースライン突合 — ドメイン参加状態 (7 項目固定)</summary>
    private static void WriteBaselineDomainStatusSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, BaselineComparisonReport> reports)
    {
        var ws = workbook.Worksheets.Add("ベースライン_ドメイン");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 2, "項目", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 3, "期待値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 4, "実測値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 5, "判定", BaselineHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc, out var report)) continue;
            if (report.DomainStatusComparison is null) continue;

            foreach (var item in report.DomainStatusComparison.Items)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = item.ItemName;
                ws.Cell(row, 3).Value = item.Expected;
                ws.Cell(row, 4).Value = item.Actual;
                ApplyVerificationStatusToCell(ws.Cell(row, 5), item.Status, ws.Range(row, 1, row, 5));
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    /// <summary>ベースライン突合 — Win/Office ライセンス (8 項目固定)</summary>
    private static void WriteBaselineLicenseSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<PcEvidence, BaselineComparisonReport> reports)
    {
        var ws = workbook.Worksheets.Add("ベースライン_ライセンス");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 2, "項目", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 3, "期待値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 4, "実測値", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 5, "判定", BaselineHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc, out var report)) continue;
            if (report.LicenseComparison is null) continue;

            foreach (var item in report.LicenseComparison.Items)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = item.ItemName;
                ws.Cell(row, 3).Value = item.Expected;
                ws.Cell(row, 4).Value = item.Actual;
                ApplyVerificationStatusToCell(ws.Cell(row, 5), item.Status, ws.Range(row, 1, row, 5));
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    /// <summary>
    /// VerificationStatus を Excel セルに反映する共通ヘルパ
    /// (SystemInfo / Checklist VerifyItems / InstalledApps / License で共用)。
    /// </summary>
    private static void ApplyVerificationStatusToCell(
        IXLCell statusCell,
        VerificationStatus status,
        IXLRange? rowRangeForMismatch)
    {
        switch (status)
        {
            case VerificationStatus.Match:
                statusCell.Value = "OK";
                statusCell.Style.Font.FontColor = MatchFont;
                statusCell.Style.Fill.BackgroundColor = MatchBg;
                break;
            case VerificationStatus.Mismatch:
                statusCell.Value = "NG";
                statusCell.Style.Font.FontColor = MismatchFont;
                statusCell.Style.Font.Bold = true;
                statusCell.Style.Fill.BackgroundColor = MismatchBg;
                rowRangeForMismatch?.Style.Fill.SetBackgroundColor(MismatchBg);
                break;
            case VerificationStatus.NoExpected:
                // ベースラインなし、対象 PC のみに値あり = 「追加された」
                statusCell.Value = "NEW";
                statusCell.Style.Font.FontColor = XLColor.FromHtml("#1565C0");
                break;
            case VerificationStatus.NoActual:
                // ベースラインあり、対象 PC に値なし = 「欠損」
                statusCell.Value = "---";
                statusCell.Style.Font.FontColor = XLColor.FromHtml("#E65100");
                statusCell.Style.Font.Bold = true;
                rowRangeForMismatch?.Style.Fill.SetBackgroundColor(MismatchBg);
                break;
        }
    }

    // ================================================================
    // Sheet 6: ユーザー・グループ
    // ================================================================

    private static readonly XLColor UserGroupHeaderBg = XLColor.FromHtml("#00695C"); // ティール

    private static void WriteUserGroupSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("ユーザー・グループ");

        // --- ローカルユーザー セクション ---
        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", UserGroupHeaderBg);
        SetHeaderCell(ws, 1, 2, "種別", UserGroupHeaderBg);
        SetHeaderCell(ws, 1, 3, "名前", UserGroupHeaderBg);
        SetHeaderCell(ws, 1, 4, "有効", UserGroupHeaderBg);
        SetHeaderCell(ws, 1, 5, "フルネーム", UserGroupHeaderBg);
        SetHeaderCell(ws, 1, 6, "説明", UserGroupHeaderBg);
        SetHeaderCell(ws, 1, 7, "ソース", UserGroupHeaderBg);
        SetHeaderCell(ws, 1, 8, "最終ログオン", UserGroupHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var user in pc.LocalUsers)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = "ユーザー";
                ws.Cell(row, 3).Value = user.Name;
                ws.Cell(row, 4).Value = user.Enabled ? "有効" : "無効";
                if (!user.Enabled) ws.Cell(row, 4).Style.Font.FontColor = XLColor.Gray;
                ws.Cell(row, 5).Value = user.FullName;
                ws.Cell(row, 6).Value = TruncateForCell(user.Description);
                ws.Cell(row, 7).Value = user.PrincipalSource;
                ws.Cell(row, 8).Value = user.LastLogon;
                row++;
            }

            foreach (var group in pc.LocalGroups)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = "グループ";
                ws.Cell(row, 3).Value = group.Name;
                ws.Cell(row, 4).Value = "";
                ws.Cell(row, 5).Value = "";
                ws.Cell(row, 6).Value = TruncateForCell(group.Description);
                row++;
            }

            foreach (var member in pc.LocalGroupMembers)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = "メンバー";
                ws.Cell(row, 3).Value = $"{member.GroupName} → {member.MemberName}";
                ws.Cell(row, 4).Value = "";
                ws.Cell(row, 5).Value = member.ObjectClass;
                ws.Cell(row, 6).Value = "";
                ws.Cell(row, 7).Value = member.PrincipalSource;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 8);
    }

    // ================================================================
    // Sheet 7: ドメイン状態
    // ================================================================

    private static readonly XLColor DomainHeaderBg = XLColor.FromHtml("#37474F"); // ダークグレー

    private static void WriteDomainStatusSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("ドメイン状態");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", DomainHeaderBg);
        SetHeaderCell(ws, 1, 2, "PartOfDomain", DomainHeaderBg);
        SetHeaderCell(ws, 1, 3, "ドメイン(WORKGROUP)", DomainHeaderBg);
        SetHeaderCell(ws, 1, 4, "ドメインロール", DomainHeaderBg);
        SetHeaderCell(ws, 1, 5, "現在のユーザー", DomainHeaderBg);
        SetHeaderCell(ws, 1, 6, "AzureAD参加", DomainHeaderBg);
        SetHeaderCell(ws, 1, 7, "ドメイン参加", DomainHeaderBg);
        SetHeaderCell(ws, 1, 8, "ドメイン名", DomainHeaderBg);
        SetHeaderCell(ws, 1, 9, "テナント名", DomainHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (pc.DomainStatus is null) continue;
            ws.Cell(row, 1).Value = pc.PcNameDisplay;
            ws.Cell(row, 2).Value = pc.DomainStatus.PartOfDomain ? "YES" : "NO";
            ws.Cell(row, 3).Value = pc.DomainStatus.Domain;
            ws.Cell(row, 4).Value = pc.DomainStatus.DomainRole;
            ws.Cell(row, 5).Value = pc.DomainStatus.CurrentUser;
            ws.Cell(row, 6).Value = pc.DomainStatus.AzureAdJoined ? "YES" : "NO";
            ws.Cell(row, 7).Value = pc.DomainStatus.DomainJoined ? "YES" : "NO";
            ws.Cell(row, 8).Value = pc.DomainStatus.DomainName;
            ws.Cell(row, 9).Value = pc.DomainStatus.TenantName;
            row++;
        }

        ApplyStyles(ws, row - 2, 9);
    }

    // ================================================================
    // Sheet 8: FWプロファイル
    // ================================================================

    private static readonly XLColor FwHeaderBg = XLColor.FromHtml("#D84315"); // ディープオレンジ

    private static void WriteFirewallProfileSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("FWプロファイル");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", FwHeaderBg);
        SetHeaderCell(ws, 1, 2, "プロファイル", FwHeaderBg);
        SetHeaderCell(ws, 1, 3, "有効", FwHeaderBg);
        SetHeaderCell(ws, 1, 4, "受信デフォルト", FwHeaderBg);
        SetHeaderCell(ws, 1, 5, "送信デフォルト", FwHeaderBg);
        SetHeaderCell(ws, 1, 6, "ログファイル", FwHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var profile in pc.FirewallProfiles)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = profile.Name;
                ws.Cell(row, 3).Value = profile.Enabled ? "有効" : "無効";
                ws.Cell(row, 4).Value = profile.DefaultInboundAction;
                ws.Cell(row, 5).Value = profile.DefaultOutboundAction;
                ws.Cell(row, 6).Value = profile.LogFileName;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 6);
    }

    // ================================================================
    // Sheet 9: FWルール
    // ================================================================

    private static void WriteFirewallRuleSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("FWルール");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", FwHeaderBg);
        SetHeaderCell(ws, 1, 2, "ルール名", FwHeaderBg);
        SetHeaderCell(ws, 1, 3, "有効", FwHeaderBg);
        SetHeaderCell(ws, 1, 4, "方向", FwHeaderBg);
        SetHeaderCell(ws, 1, 5, "アクション", FwHeaderBg);
        SetHeaderCell(ws, 1, 6, "プロファイル", FwHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var rule in pc.FirewallRules)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = rule.DisplayName;
                ws.Cell(row, 3).Value = rule.Enabled ? "有効" : "無効";
                ws.Cell(row, 4).Value = rule.Direction;
                ws.Cell(row, 5).Value = rule.Action;
                ws.Cell(row, 6).Value = rule.Profile;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 6);
    }

    // ================================================================
    // Sheet 10: オプション機能
    // ================================================================

    private static readonly XLColor FeatureHeaderBg = XLColor.FromHtml("#6A1B9A"); // パープル

    private static void WriteOptionalFeatureSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("オプション機能");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", FeatureHeaderBg);
        SetHeaderCell(ws, 1, 2, "機能名", FeatureHeaderBg);
        SetHeaderCell(ws, 1, 3, "状態", FeatureHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var feature in pc.OptionalFeatures)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = feature.FeatureName;
                var stateCell = ws.Cell(row, 3);
                stateCell.Value = feature.State;
                if (feature.State.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                    stateCell.Style.Font.FontColor = MatchFont;
                else
                    stateCell.Style.Font.FontColor = XLColor.Gray;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 3);
    }

    // ================================================================
    // Sheet 11: ユーザープロファイル
    // ================================================================

    private static readonly XLColor ProfileHeaderBg = XLColor.FromHtml("#00838F"); // シアン

    private static void WriteUserProfileSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("ユーザープロファイル");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", ProfileHeaderBg);
        SetHeaderCell(ws, 1, 2, "ローカルパス", ProfileHeaderBg);
        SetHeaderCell(ws, 1, 3, "SID", ProfileHeaderBg);
        SetHeaderCell(ws, 1, 4, "最終使用日時", ProfileHeaderBg);
        SetHeaderCell(ws, 1, 5, "ロード済み", ProfileHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var profile in pc.UserProfiles)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = profile.LocalPath;
                ws.Cell(row, 3).Value = profile.SID;
                ws.Cell(row, 4).Value = profile.LastUseTime;
                ws.Cell(row, 5).Value = profile.Loaded ? "YES" : "NO";
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    // ================================================================
    // Sheet 12: ディスク・パーティション
    // ================================================================

    private static readonly XLColor DiskHeaderBg = XLColor.FromHtml("#4E342E"); // ブラウン

    private static void WriteDiskPartitionSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("ディスク・パーティション");

        // --- ディスク セクション ---
        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", DiskHeaderBg);
        SetHeaderCell(ws, 1, 2, "種別", DiskHeaderBg);
        SetHeaderCell(ws, 1, 3, "番号", DiskHeaderBg);
        SetHeaderCell(ws, 1, 4, "名前", DiskHeaderBg);
        SetHeaderCell(ws, 1, 5, "シリアル番号", DiskHeaderBg);
        SetHeaderCell(ws, 1, 6, "サイズ(GB)", DiskHeaderBg);
        SetHeaderCell(ws, 1, 7, "パーティションスタイル", DiskHeaderBg);
        SetHeaderCell(ws, 1, 8, "正常性", DiskHeaderBg);
        SetHeaderCell(ws, 1, 9, "動作状態", DiskHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var disk in pc.Disks)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = "ディスク";
                ws.Cell(row, 3).Value = disk.Number;
                ws.Cell(row, 4).Value = disk.FriendlyName;
                ws.Cell(row, 5).Value = disk.SerialNumber;
                ws.Cell(row, 6).Value = disk.SizeGB;
                ws.Cell(row, 7).Value = disk.PartitionStyle;
                ws.Cell(row, 8).Value = disk.HealthStatus;
                ws.Cell(row, 9).Value = disk.OperationalStatus;
                row++;
            }

            foreach (var part in pc.Partitions)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = "パーティション";
                ws.Cell(row, 3).Value = $"{part.DiskNumber}-{part.PartitionNumber}";
                ws.Cell(row, 4).Value = part.DriveLetter;
                ws.Cell(row, 5).Value = "";
                ws.Cell(row, 6).Value = part.SizeGB;
                ws.Cell(row, 7).Value = part.Type;
                ws.Cell(row, 8).Value = part.IsSystem ? "System" : "";
                ws.Cell(row, 9).Value = part.IsBoot ? "Boot" : "";
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 9);
    }

    // ================================================================
    // Sheet 13: 電源設定
    // ================================================================

    private static readonly XLColor PowerHeaderBg = XLColor.FromHtml("#827717"); // オリーブ

    private static void WritePowerSettingsSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("電源設定");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", PowerHeaderBg);
        SetHeaderCell(ws, 1, 2, "アクティブプラン", PowerHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (pc.PowerSettings is null) continue;
            ws.Cell(row, 1).Value = pc.PcNameDisplay;
            ws.Cell(row, 2).Value = pc.PowerSettings.ActivePlanName;
            row++;
        }

        ApplyStyles(ws, row - 2, 2);
    }

    // ================================================================
    // Sheet 14: WiFiプロファイル
    // ================================================================

    private static readonly XLColor WiFiHeaderBg = XLColor.FromHtml("#1565C0"); // ブルー

    private static void WriteWiFiProfileSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("WiFiプロファイル");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", WiFiHeaderBg);
        SetHeaderCell(ws, 1, 2, "プロファイル名", WiFiHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var profile in pc.WiFiProfiles)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = profile;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 2);
    }

    // ================================================================
    // Sheet 15: 復元ポイント
    // ================================================================

    private static readonly XLColor RestoreHeaderBg = XLColor.FromHtml("#AD1457"); // ピンク

    private static void WriteRestorePointSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("復元ポイント");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", RestoreHeaderBg);
        SetHeaderCell(ws, 1, 2, "シーケンス番号", RestoreHeaderBg);
        SetHeaderCell(ws, 1, 3, "説明", RestoreHeaderBg);
        SetHeaderCell(ws, 1, 4, "種類", RestoreHeaderBg);
        SetHeaderCell(ws, 1, 5, "作成日時", RestoreHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var rp in pc.RestorePoints)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = rp.SequenceNumber;
                ws.Cell(row, 3).Value = TruncateForCell(rp.Description);
                ws.Cell(row, 4).Value = rp.RestorePointType;
                ws.Cell(row, 5).Value = rp.CreationTime;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    // ================================================================
    // Sheet 16: Windows Defender
    // ================================================================

    private static readonly XLColor DefenderHeaderBg = XLColor.FromHtml("#1B5E20"); // ダークグリーン

    private static void WriteDefenderStatusSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("Windows Defender");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 2, "AMService", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 3, "Antispyware", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 4, "Antivirus", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 5, "リアルタイム保護", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 6, "動作監視", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 7, "IOAV保護", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 8, "NIS", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 9, "オンアクセス保護", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 10, "エンジンVer", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 11, "製品Ver", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 12, "定義Ver", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 13, "定義更新日", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 14, "クイックスキャン", DefenderHeaderBg);
        SetHeaderCell(ws, 1, 15, "フルスキャン", DefenderHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (pc.DefenderStatus is null) continue;
            var d = pc.DefenderStatus;
            ws.Cell(row, 1).Value = pc.PcNameDisplay;
            ws.Cell(row, 2).Value = d.AMServiceEnabled ? "ON" : "OFF";
            ws.Cell(row, 3).Value = d.AntispywareEnabled ? "ON" : "OFF";
            ws.Cell(row, 4).Value = d.AntivirusEnabled ? "ON" : "OFF";
            ws.Cell(row, 5).Value = d.RealTimeProtectionEnabled ? "ON" : "OFF";
            ws.Cell(row, 6).Value = d.BehaviorMonitorEnabled ? "ON" : "OFF";
            ws.Cell(row, 7).Value = d.IoavProtectionEnabled ? "ON" : "OFF";
            ws.Cell(row, 8).Value = d.NISEnabled ? "ON" : "OFF";
            ws.Cell(row, 9).Value = d.OnAccessProtectionEnabled ? "ON" : "OFF";

            // ON/OFF セルの色分け
            for (var col = 2; col <= 9; col++)
            {
                var cell = ws.Cell(row, col);
                if (cell.GetString() == "ON")
                    cell.Style.Font.FontColor = MatchFont;
                else
                {
                    cell.Style.Font.FontColor = MismatchFont;
                    cell.Style.Font.Bold = true;
                }
            }

            ws.Cell(row, 10).Value = d.AMEngineVersion;
            ws.Cell(row, 11).Value = d.AMProductVersion;
            ws.Cell(row, 12).Value = d.AntivirusSignatureVersion;
            ws.Cell(row, 13).Value = d.AntivirusSignatureLastUpdated;
            ws.Cell(row, 14).Value = d.QuickScanEndTime;
            ws.Cell(row, 15).Value = d.FullScanEndTime;
            row++;
        }

        ApplyStyles(ws, row - 2, 15);
    }

    // ================================================================
    // Sheet 17: Windows Update
    // ================================================================

    private static readonly XLColor UpdateHeaderBg = XLColor.FromHtml("#283593"); // インディゴ

    private static void WriteWindowsUpdateSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("Windows Update");

        SetHeaderCell(ws, 1, 1, "PC名 (計画|実)", UpdateHeaderBg);
        SetHeaderCell(ws, 1, 2, "HotFixID", UpdateHeaderBg);
        SetHeaderCell(ws, 1, 3, "説明", UpdateHeaderBg);
        SetHeaderCell(ws, 1, 4, "インストール者", UpdateHeaderBg);
        SetHeaderCell(ws, 1, 5, "インストール日", UpdateHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var update in pc.WindowsUpdates)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = update.HotFixID;
                ws.Cell(row, 3).Value = TruncateForCell(update.Description);
                ws.Cell(row, 4).Value = update.InstalledBy;
                ws.Cell(row, 5).Value = update.InstalledOn;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 5);
    }

    // ================================================================
    // Windowsライセンス (§21)
    // ================================================================

    private static void WriteWindowsLicenseSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("Windowsライセンス");
        ws.TabColor = LicenseHeaderBg;

        var headers = new[]
        {
            "PC名 (計画|実)", "シリアル番号", "収集日",
            "プロダクト名", "ライセンス状態", "プロダクトキー末尾",
            "ライセンスファミリ", "プロダクトキーチャネル", "残り猶予期間(分)",
            "KMS configured", "KMS discovered",
            "ClientMachineID", "KMS Machine", "KMS Port",
            "OA3 Original Key", "OA3 Key Description", "PolicyCacheRefreshRequired",
        };
        for (var i = 0; i < headers.Length; i++)
            SetHeaderCell(ws, 1, i + 1, headers[i], LicenseHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            var lic = pc.WindowsLicense;
            if (lic is null) continue;

            var products = lic.Products;
            if (products.Count == 0)
            {
                WriteWindowsLicenseRow(ws, row, pc, product: null, lic.Service);
                row++;
                continue;
            }

            foreach (var product in products)
            {
                WriteWindowsLicenseRow(ws, row, pc, product, lic.Service);
                row++;
            }
        }

        ApplyStyles(ws, row - 2, headers.Length);
    }

    private static void WriteWindowsLicenseRow(
        IXLWorksheet ws, int row, PcEvidence pc,
        WindowsLicensingProduct? product, WindowsLicensingService? service)
    {
        ws.Cell(row, 1).Value = pc.PcNameDisplay;
        ws.Cell(row, 2).Value = pc.SerialNumber;
        ws.Cell(row, 3).Value = pc.CollectionDate;

        if (product is null)
        {
            ws.Cell(row, 4).Value = "(プロダクトキー未検出)";
            ws.Cell(row, 5).Value = "未取得";
        }
        else
        {
            ws.Cell(row, 4).Value = product.Name;
            ws.Cell(row, 5).Value = $"{product.LicenseStatusText} ({product.LicenseStatusCode})";
            ws.Cell(row, 6).Value = product.PartialProductKey;
            ws.Cell(row, 7).Value = product.LicenseFamily;
            ws.Cell(row, 8).Value = product.ProductKeyChannel;
            if (product.GracePeriodMinutes.HasValue)
                ws.Cell(row, 9).Value = product.GracePeriodMinutes.Value;
            ws.Cell(row, 10).Value = product.KmsConfigured ?? string.Empty;
            ws.Cell(row, 11).Value = product.KmsDiscovered ?? string.Empty;
        }

        if (service is not null)
        {
            ws.Cell(row, 12).Value = service.ClientMachineID;
            ws.Cell(row, 13).Value = service.KeyManagementServiceMachine;
            ws.Cell(row, 14).Value = service.KeyManagementServicePort;
            ws.Cell(row, 15).Value = service.OA3xOriginalProductKey;
            ws.Cell(row, 16).Value = service.OA3xOriginalProductKeyDescription;
            ws.Cell(row, 17).Value = service.PolicyCacheRefreshRequired;
        }

        // 監査着色: Licensed (1) 以外は赤背景 + 赤文字
        var isAnomaly = product is null
            || product.LicenseStatusCode != WindowsLicensingProduct.LicensedStatusCode;
        if (isAnomaly)
        {
            var range = ws.Range(row, 1, row, 17);
            range.Style.Fill.BackgroundColor = MismatchBg;
            range.Style.Font.FontColor = MismatchFont;
        }
    }

    // ================================================================
    // Officeライセンス (§22)
    // ================================================================

    /// <summary>Officeライセンスシート列数 (M11: 18 → 23 列、新規 5 列を 5-9 列目に挿入)</summary>
    private const int OfficeLicenseSheetColumns = 23;

    private static void WriteOfficeLicenseSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("Officeライセンス");
        ws.TabColor = LicenseHeaderBg;

        // M11: vNext サマリ列 (5-9) を OSPP 詳細 (17-23) の前に挿入。
        // Verdict は manifest §22 status を権威とし、再判定はしない。
        var headers = new[]
        {
            "PC名 (計画|実)", "シリアル番号", "収集日",
            "Office インストール状態",
            "SubscriptionType", "Verdict", "Conclusion", "LicensedUsers", "TenantId",
            "C2R ProductReleaseIds", "C2R バージョン", "C2R Platform",
            "C2R UpdateChannel", "C2R AudienceData", "C2R ClientCulture",
            "OSPP パス",
            "LICENSE NAME", "LICENSE DESCRIPTION", "ライセンス状態",
            "プロダクトキー末尾", "ERROR CODE", "REMAINING GRACE", "KMS Machine",
        };
        for (var i = 0; i < headers.Length; i++)
            SetHeaderCell(ws, 1, i + 1, headers[i], LicenseHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            var lic = pc.OfficeLicense;
            if (lic is null) continue;

            var verdict = pc.Manifest?.Sections.FirstOrDefault(s => s.Id == "22")?.Status;

            if (!lic.IsOfficeInstalled || lic.Products.Count == 0)
            {
                // 未インストール / C2R あり OSPP 製品ブロック空 のいずれも 1 行
                WriteOfficeLicenseRow(ws, row, pc, lic, product: null, verdict);
                row++;
                continue;
            }

            foreach (var product in lic.Products)
            {
                WriteOfficeLicenseRow(ws, row, pc, lic, product, verdict);
                row++;
            }
        }

        ApplyStyles(ws, row - 2, headers.Length);
    }

    private static void WriteOfficeLicenseRow(
        IXLWorksheet ws, int row, PcEvidence pc,
        OfficeLicenseData lic, OfficeProductLicense? product, SectionStatus? verdict)
    {
        ws.Cell(row, 1).Value = pc.PcNameDisplay;
        ws.Cell(row, 2).Value = pc.SerialNumber;
        ws.Cell(row, 3).Value = pc.CollectionDate;
        ws.Cell(row, 4).Value = lic.IsOfficeInstalled ? "インストール済み" : "未インストール";

        // ---- 5-9: M11 vNext サマリブロック ----
        ws.Cell(row, 5).Value = ResolveSubscriptionType(lic);
        ws.Cell(row, 6).Value = verdict?.ToString() ?? "(manifest 不在)";
        ws.Cell(row, 7).Value = lic.InterpretationConclusion ?? string.Empty;
        ws.Cell(row, 8).Value = string.Join(
            ", ",
            lic.VnextEntries.Where(e => e.IsProvisioned).Select(e => e.UserProfile));
        ws.Cell(row, 9).Value = lic.VnextEntries
            .FirstOrDefault(e => e.IsProvisioned && !string.IsNullOrEmpty(e.TenantId))
            ?.TenantId ?? string.Empty;

        // ---- 10-15: C2R 詳細 ----
        var c2r = lic.ClickToRun;
        if (c2r is not null)
        {
            ws.Cell(row, 10).Value = c2r.ProductReleaseIds;
            ws.Cell(row, 11).Value = c2r.VersionToReport;
            ws.Cell(row, 12).Value = c2r.Platform;
            ws.Cell(row, 13).Value = c2r.UpdateChannel;
            ws.Cell(row, 14).Value = c2r.AudienceData;
            ws.Cell(row, 15).Value = c2r.ClientCulture;
        }

        // ---- 16: OSPP パス ----
        ws.Cell(row, 16).Value = lic.OsppPath ?? string.Empty;

        // ---- 17-23: OSPP 製品詳細 ----
        if (product is not null)
        {
            ws.Cell(row, 17).Value = product.LicenseName;
            ws.Cell(row, 18).Value = product.LicenseDescription;
            ws.Cell(row, 19).Value = product.LicenseStatusText;
            ws.Cell(row, 20).Value = product.LastFiveCharsProductKey;
            ws.Cell(row, 21).Value = product.ErrorCode;
            ws.Cell(row, 22).Value = product.RemainingGrace;
            ws.Cell(row, 23).Value = product.KmsMachineName;
        }

        ApplyOfficeLicenseRowColor(ws, row, lic, product, verdict);
    }

    /// <summary>
    /// 行全体に着色する。manifest §22 status が真値、null (旧 evidence) のみ
    /// product.IsLicensed をフォールバック判定に使う。
    /// </summary>
    private static void ApplyOfficeLicenseRowColor(
        IXLWorksheet ws, int row, OfficeLicenseData lic,
        OfficeProductLicense? product, SectionStatus? verdict)
    {
        var range = ws.Range(row, 1, row, OfficeLicenseSheetColumns);

        if (verdict is null)
        {
            // 旧 evidence: manifest §22 不在。OSPP 製品の Licensed/非 Licensed で従来判定
            if (!lic.IsOfficeInstalled)
            {
                range.Style.Fill.BackgroundColor = InfoBg;
            }
            else if (product is null || !product.IsLicensed)
            {
                range.Style.Fill.BackgroundColor = MismatchBg;
                range.Style.Font.FontColor = MismatchFont;
            }
            return;
        }

        switch (verdict.Value)
        {
            case SectionStatus.Failed:
                range.Style.Fill.BackgroundColor = MismatchBg;
                range.Style.Font.FontColor = MismatchFont;
                break;

            case SectionStatus.Partial:
                range.Style.Fill.BackgroundColor = WarningBg;
                break;

            case SectionStatus.Success:
                if (!lic.IsOfficeInstalled)
                    range.Style.Fill.BackgroundColor = InfoBg;
                else
                    range.Style.Fill.BackgroundColor = MatchBg;
                break;

            case SectionStatus.Skipped:
                range.Style.Font.FontColor = XLColor.Gray;
                break;
        }
    }

    /// <summary>
    /// SubscriptionType 列の表示文字列を算出する。
    /// "M365 Subscription" / "Volume" / "Buy-once" / "Not installed" / "Unknown"。
    /// </summary>
    private static string ResolveSubscriptionType(OfficeLicenseData lic)
    {
        if (!lic.IsOfficeInstalled) return "Not installed";
        if (lic.IsSubscriptionDetected is null) return "Unknown";
        if (lic.IsSubscriptionDetected.Value) return "M365 Subscription";

        var ids = lic.ClickToRun?.ProductReleaseIds ?? string.Empty;
        return ids.Contains("Volume", StringComparison.OrdinalIgnoreCase)
            ? "Volume"
            : "Buy-once";
    }

    // ================================================================
    // セキュリティベースライン (§23) — M12
    // ================================================================

    private const int SecurityBaselineSheetColumns = 10;

    private static void WriteSecurityBaselineSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("セキュリティベースライン");
        ws.TabColor = LicenseHeaderBg;

        var headers = new[]
        {
            "PC名 (計画|実)",
            "TPM Present", "TPM Ready",
            "SecureBoot",
            "VBS Status", "HVCI Running", "CodeIntegrity Enforcement",
            "LSA RunAsPPL",
            "BIOS Version", "BIOS Date",
        };
        for (var i = 0; i < headers.Length; i++)
            SetHeaderCell(ws, 1, i + 1, headers[i], LicenseHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (pc.SecurityBaseline is null) continue;
            WriteSecurityBaselineRow(ws, row, pc, pc.SecurityBaseline);
            row++;
        }

        ApplyStyles(ws, row - 2, headers.Length);
    }

    private static void WriteSecurityBaselineRow(
        IXLWorksheet ws, int row, PcEvidence pc, SecurityBaselineData sb)
    {
        ws.Cell(row, 1).Value = pc.PcNameDisplay;

        // ---- 2-3: TPM ----
        WriteAuditableBool(ws.Cell(row, 2), sb.Tpm?.Present, badIfFalse: true);
        WriteAuditableBool(ws.Cell(row, 3), sb.Tpm?.Ready, badIfFalse: true);  // Ready=false は赤 (M12 仕様)

        // ---- 4: SecureBoot ----
        WriteAuditableBool(ws.Cell(row, 4), sb.SecureBootEnabled, badIfFalse: false, warnIfFalse: true);

        // ---- 5: VBS Status ----
        if (sb.Vbs is null)
        {
            WriteNa(ws.Cell(row, 5));
        }
        else
        {
            ws.Cell(row, 5).Value = sb.Vbs.VbsStatusText;
            if (string.Equals(sb.Vbs.VbsStatusText, "Not running", System.StringComparison.Ordinal))
                ApplyCellAuditColor(ws.Cell(row, 5), AuditLevel.Warning);
        }

        // ---- 6: HVCI Running ----
        if (sb.Vbs is null)
            WriteNa(ws.Cell(row, 6));
        else
        {
            ws.Cell(row, 6).Value = sb.Vbs.HvciRunning;
            if (!sb.Vbs.HvciRunning) ApplyCellAuditColor(ws.Cell(row, 6), AuditLevel.Warning);
        }

        // ---- 7: CodeIntegrity Enforcement ----
        if (sb.Vbs is null)
            WriteNa(ws.Cell(row, 7));
        else
        {
            ws.Cell(row, 7).Value = sb.Vbs.CodeIntegrityEnforcementText;
            if (string.Equals(sb.Vbs.CodeIntegrityEnforcementText, "Off", System.StringComparison.Ordinal))
                ApplyCellAuditColor(ws.Cell(row, 7), AuditLevel.Warning);
        }

        // ---- 8: LSA RunAsPPL ----
        if (sb.Lsa is null)
        {
            WriteNa(ws.Cell(row, 8));
        }
        else
        {
            ws.Cell(row, 8).Value = sb.Lsa.RunAsPplRawText;
            switch (sb.Lsa.RunAsPpl)
            {
                case LsaRunAsPplState.Off:
                    ApplyCellAuditColor(ws.Cell(row, 8), AuditLevel.Bad);
                    break;
                case LsaRunAsPplState.OnWithUefiLock:
                    ApplyCellAuditColor(ws.Cell(row, 8), AuditLevel.Good);
                    break;
                case LsaRunAsPplState.Unknown:
                    ApplyCellAuditColor(ws.Cell(row, 8), AuditLevel.Na);
                    break;
                // On は通常 (着色なし)
            }
        }

        // ---- 9-10: BIOS ----
        if (sb.Bios is null)
        {
            WriteNa(ws.Cell(row, 9));
            WriteNa(ws.Cell(row, 10));
        }
        else
        {
            ws.Cell(row, 9).Value = sb.Bios.SmbiosBiosVersion;
            ws.Cell(row, 10).Value = sb.Bios.ReleaseDate;
        }
    }

    /// <summary>
    /// 監査用 bool セル書き込み + 着色。
    /// null は "N/A" + グレー、false は (badIfFalse なら赤 / warnIfFalse なら黄)、true は通常。
    /// </summary>
    private static void WriteAuditableBool(IXLCell cell, bool? value, bool badIfFalse = false, bool warnIfFalse = false)
    {
        if (value is null)
        {
            WriteNa(cell);
            return;
        }
        cell.Value = value.Value;
        if (!value.Value)
        {
            if (badIfFalse) ApplyCellAuditColor(cell, AuditLevel.Bad);
            else if (warnIfFalse) ApplyCellAuditColor(cell, AuditLevel.Warning);
        }
    }

    private static void WriteNa(IXLCell cell)
    {
        cell.Value = "N/A";
        ApplyCellAuditColor(cell, AuditLevel.Na);
    }

    private enum AuditLevel { Good, Warning, Bad, Na }

    /// <summary>セル単位の監査着色 (M12: SecurityBaseline で導入)。</summary>
    private static void ApplyCellAuditColor(IXLCell cell, AuditLevel level)
    {
        switch (level)
        {
            case AuditLevel.Good:
                cell.Style.Fill.BackgroundColor = MatchBg;
                cell.Style.Font.FontColor = MatchFont;
                break;
            case AuditLevel.Warning:
                cell.Style.Fill.BackgroundColor = WarningBg;
                break;
            case AuditLevel.Bad:
                cell.Style.Fill.BackgroundColor = MismatchBg;
                cell.Style.Font.FontColor = MismatchFont;
                cell.Style.Font.Bold = true;
                break;
            case AuditLevel.Na:
                cell.Style.Font.FontColor = XLColor.Gray;
                break;
        }
    }

    // ================================================================
    // 証明書 (§25) — M13
    // ================================================================

    private const int CertificatesSheetColumns = 12;

    private static void WriteCertificatesSheet(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var ws = workbook.Worksheets.Add("証明書");
        ws.TabColor = LicenseHeaderBg;

        var headers = new[]
        {
            "PC名 (計画|実)", "Store", "Subject", "Issuer", "Thumbprint",
            "NotBefore", "NotAfter", "DaysToExpiry",
            "HasPrivateKey", "EnhancedKeyUsageList", "FriendlyName", "SerialNumber",
        };
        for (var i = 0; i < headers.Length; i++)
            SetHeaderCell(ws, 1, i + 1, headers[i], LicenseHeaderBg);

        var today = System.DateTime.Today;
        var row = 2;
        foreach (var pc in pcEvidences)
        {
            foreach (var cert in pc.Certificates)
            {
                WriteCertificateRow(ws, row, pc, cert, today);
                row++;
            }
        }

        ApplyStyles(ws, row - 2, headers.Length);
    }

    private static void WriteCertificateRow(
        IXLWorksheet ws, int row, PcEvidence pc, CertificateEntry cert, System.DateTime today)
    {
        ws.Cell(row, 1).Value = pc.PcNameDisplay;
        ws.Cell(row, 2).Value = cert.Store;
        ws.Cell(row, 3).Value = cert.Subject;
        ws.Cell(row, 4).Value = cert.Issuer;
        ws.Cell(row, 5).Value = cert.Thumbprint;
        if (cert.NotBefore.HasValue) ws.Cell(row, 6).Value = cert.NotBefore.Value;
        if (cert.NotAfter.HasValue) ws.Cell(row, 7).Value = cert.NotAfter.Value;

        // ---- 8: DaysToExpiry + 着色 ----
        var days = cert.DaysToExpiry(today);
        if (days.HasValue)
        {
            ws.Cell(row, 8).Value = days.Value;
            var level = days.Value switch
            {
                < 0 => AuditLevel.Bad,           // 期限切れ
                <= 30 => AuditLevel.Warning,     // オレンジ系警告に近い、ここでは黄
                <= 90 => AuditLevel.Warning,     // 同上 (黄)
                _ => AuditLevel.Good,            // 90+ は通常で十分だが視認性のため緑側
            };
            // 仕様: 期限切れ=赤 / 0〜30=オレンジ寄り / 30〜90=黄 / 90+=通常
            // M12 の WarningBg (薄黄) を使い、期限切れだけ赤、それ以外は通常 or 黄。
            if (level == AuditLevel.Bad)
                ApplyCellAuditColor(ws.Cell(row, 8), AuditLevel.Bad);
            else if (days.Value <= 90)
                ApplyCellAuditColor(ws.Cell(row, 8), AuditLevel.Warning);
        }
        else
        {
            ws.Cell(row, 8).Value = "N/A";
            ApplyCellAuditColor(ws.Cell(row, 8), AuditLevel.Na);
        }

        ws.Cell(row, 9).Value = cert.HasPrivateKey;
        ws.Cell(row, 10).Value = cert.EnhancedKeyUsageList;
        ws.Cell(row, 11).Value = cert.FriendlyName;
        ws.Cell(row, 12).Value = cert.SerialNumber;
    }

    // ================================================================
    // 未知 section ID (manifest 経由・未対応) — M5+M7
    // ================================================================

    /// <summary>
    /// EvidenceConstants.KnownSections に無い manifest section ID を、
    /// 1 ID = 1 シートで raw 表示する。
    /// シート名は "§{Id} {Title}" (Excel 制限 31 文字に切り詰め)。
    /// タブ色は黄色 (警告)、A1 セルに manager の AssemblyVersion を記録する。
    /// </summary>
    private static void WriteUnknownSectionSheets(IXLWorkbook workbook, IReadOnlyList<PcEvidence> pcEvidences)
    {
        var groups = pcEvidences
            .SelectMany(pc => pc.UnknownSections.Select(u => (Pc: pc, Section: u)))
            .GroupBy(t => t.Section.Id, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var first = group.First().Section;
            var sheetName = SanitizeSheetName($"§{first.Id} {first.Title}");
            var ws = workbook.Worksheets.Add(sheetName);
            ws.TabColor = XLColor.Yellow;

            // ヘッダブロック (manifest 経由の前方互換情報)
            ws.Cell(1, 1).Value = $"fabriq_evidence_manager v{ManagerVersion} (manifest 経由・未対応セクション)";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#7B1FA2");
            ws.Cell(2, 1).Value = $"§{first.Id} {first.Title}";
            ws.Cell(2, 1).Style.Font.Bold = true;

            // データテーブルヘッダ (4 行目)
            const int headerRow = 4;
            SetHeaderCell(ws, headerRow, 1, "PC名 (計画|実)", XLColor.FromHtml("#7B1FA2"));
            SetHeaderCell(ws, headerRow, 2, "ファイル名", XLColor.FromHtml("#7B1FA2"));
            SetHeaderCell(ws, headerRow, 3, "種別", XLColor.FromHtml("#7B1FA2"));
            SetHeaderCell(ws, headerRow, 4, "raw 内容", XLColor.FromHtml("#7B1FA2"));

            var row = headerRow + 1;
            foreach (var (pc, section) in group)
            {
                ws.Cell(row, 1).Value = pc.PcNameDisplay;
                ws.Cell(row, 2).Value = Path.GetFileName(section.FilePath);
                ws.Cell(row, 3).Value = section.FileType.ToString();
                ws.Cell(row, 4).Value = TruncateForCell(section.RawContent);
                ws.Cell(row, 4).Style.Alignment.WrapText = true;
                ws.Cell(row, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                row++;
            }

            // 列幅: PC名 / ファイル名 / 種別 は contents、raw 内容 は固定幅 80
            ws.Column(1).AdjustToContents();
            ws.Column(2).AdjustToContents();
            ws.Column(3).AdjustToContents();
            ws.Column(4).Width = 80;

            // 罫線 + ヘッダ固定
            if (row > headerRow + 1)
            {
                var dataRange = ws.Range(headerRow, 1, row - 1, 4);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }
            ws.SheetView.FreezeRows(headerRow);
        }
    }

    /// <summary>Excel シート名の禁則文字を除去し 31 文字に切り詰める。</summary>
    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var clean = string.Concat(name.Where(c => !invalid.Contains(c)));
        return clean.Length > 31 ? clean[..31] : clean;
    }

    // ================================================================
    // 共通ヘルパー
    // ================================================================

    private static void SetHeaderCell(IXLWorksheet ws, int row, int col, string value, XLColor bgColor)
    {
        var cell = ws.Cell(row, col);
        cell.Value = value;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = HeaderFontColor;
        cell.Style.Font.FontName = "Arial";
        cell.Style.Font.FontSize = 10;
        cell.Style.Fill.BackgroundColor = bgColor;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.WrapText = true;
    }

    private static void ApplyStatusColor(IXLCell cell, string status)
    {
        var upper = status.ToUpperInvariant();
        if (upper is "OK" or "SUCCESS" or "MATCH" or "PASS")
        {
            cell.Style.Font.FontColor = MatchFont;
        }
        else if (upper is "NG" or "ERROR" or "MISMATCH" or "FAIL")
        {
            cell.Style.Font.FontColor = MismatchFont;
            cell.Style.Font.Bold = true;
        }
        else if (upper is "SKIP" or "SKIPPED" or "-" or "NOTRUN")
        {
            cell.Style.Font.FontColor = XLColor.Gray;
        }
    }

    private static void ApplyStyles(IXLWorksheet ws, int dataRowCount, int totalColumns)
        => ApplyStyles(ws, dataRowCount, totalColumns, headerRow: 1);

    /// <summary>
    /// 罫線・フォント・列幅・ヘッダ固定・オートフィルタを適用する。
    /// headerRow 以前のメタデータブロックには影響しない。
    /// </summary>
    private static void ApplyStyles(IXLWorksheet ws, int dataRowCount, int totalColumns, int headerRow)
    {
        if (dataRowCount == 0 || totalColumns == 0)
            return;

        var lastRow = headerRow + dataRowCount;

        // ヘッダ + データ行に罫線
        var dataRange = ws.Range(headerRow, 1, lastRow, totalColumns);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // データ行のフォント
        var bodyRange = ws.Range(headerRow + 1, 1, lastRow, totalColumns);
        bodyRange.Style.Font.FontName = "Arial";
        bodyRange.Style.Font.FontSize = 10;

        // 列幅自動調整 (最大幅制限)
        for (var col = 1; col <= totalColumns; col++)
        {
            ws.Column(col).AdjustToContents();
            if (ws.Column(col).Width > EvidenceConstants.ExcelMaxColumnWidth)
                ws.Column(col).Width = EvidenceConstants.ExcelMaxColumnWidth;
        }

        // ヘッダ行を固定 (メタデータブロックも一緒にスクロール固定する)
        ws.SheetView.FreezeRows(headerRow);

        // オートフィルタはヘッダ + データ範囲のみ
        ws.Range(headerRow, 1, lastRow, totalColumns).SetAutoFilter();
    }
}
