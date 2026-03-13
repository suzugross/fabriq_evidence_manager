using System;
using System.Collections.Generic;
using System.Linq;
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
    // 固定列数 (PC名, シリアル番号, 収集日, 突合判定)
    private const int FixedColumnCount = 4;

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
    private static readonly XLColor HeaderFontColor = XLColor.White;

    // ステータス色
    private static readonly XLColor MatchBg = XLColor.FromHtml("#E2EFDA");       // 薄緑
    private static readonly XLColor MismatchBg = XLColor.FromHtml("#FCE4EC");    // 薄赤
    private static readonly XLColor MismatchFont = XLColor.FromHtml("#C62828");  // 赤文字
    private static readonly XLColor MatchFont = XLColor.FromHtml("#2E7D32");     // 緑文字

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
                           + allDriveLetters.Count * DriveSubColumnCount;

        using var workbook = new XLWorkbook();

        // ---- Sheet 1: PC情報一覧 ----
        var ws = workbook.Worksheets.Add(EvidenceConstants.ExcelSheetName);
        WriteMainHeader(ws, maxNicCount, allDriveLetters);

        for (var i = 0; i < pcEvidences.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteMainDataRow(ws, row: i + 2, pcEvidences[i], maxNicCount, allDriveLetters, options);
        }

        ApplyStyles(ws, pcEvidences.Count, totalColumns);

        // ---- Sheet 2: 突合詳細 ----
        if (options?.VerificationReports is { Count: > 0 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteVerificationSheet(workbook, pcEvidences, options.VerificationReports);
        }

        // ---- Sheet 3: チェックリスト ----
        if (options?.ChecklistResults is { Count: > 0 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteChecklistSheet(workbook, pcEvidences, options.ChecklistResults);
        }

        // ---- Sheet 4: 実行履歴 ----
        if (options?.ExportHistories is { Count: > 0 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteHistorySheet(workbook, pcEvidences, options.ExportHistories);
        }

        // ---- Sheet 5: ベースライン突合 ----
        if (options?.BaselineReports is { Count: > 0 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteBaselineSheet(workbook, pcEvidences, options.BaselineReports);
        }

        workbook.SaveAs(outputFilePath);
        return Task.CompletedTask;
    }

    // ================================================================
    // Sheet 1: PC情報一覧
    // ================================================================

    private static void WriteMainHeader(IXLWorksheet ws, int maxNicCount, List<string> driveLetters)
    {
        var col = 1;

        // 固定列
        SetHeaderCell(ws, 1, col++, "PC名", BaseHeaderBg);
        SetHeaderCell(ws, 1, col++, "シリアル番号", BaseHeaderBg);
        SetHeaderCell(ws, 1, col++, "収集日", BaseHeaderBg);
        SetHeaderCell(ws, 1, col++, "突合判定", BaseHeaderBg);

        // NIC列 (動的)
        for (var i = 1; i <= maxNicCount; i++)
        {
            SetHeaderCell(ws, 1, col++, $"NIC{i}_MAC", NicHeaderBg);
            SetHeaderCell(ws, 1, col++, $"NIC{i}_アダプタ", NicHeaderBg);
            SetHeaderCell(ws, 1, col++, $"NIC{i}_接続名", NicHeaderBg);
        }

        // BitLocker列 (動的: ドライブレター別)
        foreach (var drive in driveLetters)
        {
            SetHeaderCell(ws, 1, col++, $"{drive}ドライブ_キーID", KeyHeaderBg);
            SetHeaderCell(ws, 1, col++, $"{drive}ドライブ_回復キー", KeyHeaderBg);
        }
    }

    private static void WriteMainDataRow(
        IXLWorksheet ws, int row, PcEvidence pc,
        int maxNicCount, List<string> driveLetters,
        ExcelExportOptions? options)
    {
        var col = 1;

        // PC名, シリアル番号, 収集日
        ws.Cell(row, col++).Value = pc.PcName;
        ws.Cell(row, col++).Value = pc.SerialNumber;
        ws.Cell(row, col++).Value = pc.CollectionDate;

        // 突合判定列
        var verifyCell = ws.Cell(row, col++);
        if (options?.VerificationReports is not null
            && options.VerificationReports.TryGetValue(pc.PcName, out var report))
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

        // NIC列 (MAC順に番号付与)
        for (var i = 0; i < maxNicCount; i++)
        {
            if (i < pc.MacAddresses.Count)
            {
                var mac = pc.MacAddresses[i];
                ws.Cell(row, col++).Value = mac.PhysicalAddress;
                ws.Cell(row, col++).Value = mac.AdapterName;
                ws.Cell(row, col++).Value = mac.ConnectionName;
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
    }

    // ================================================================
    // Sheet 2: 突合詳細
    // ================================================================

    private static void WriteVerificationSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<string, VerificationReport> reports)
    {
        var ws = workbook.Worksheets.Add("突合詳細");

        // ヘッダー
        SetHeaderCell(ws, 1, 1, "PC名", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 2, "チェック項目", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 3, "期待値 (hostlist)", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 4, "実測値 (evidence)", VerifyHeaderBg);
        SetHeaderCell(ws, 1, 5, "判定", VerifyHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc.PcName, out var report)) continue;
            if (!report.HostlistFound) continue;

            foreach (var item in report.Items)
            {
                ws.Cell(row, 1).Value = pc.PcName;
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
        IReadOnlyDictionary<string, ChecklistResult> results)
    {
        var ws = workbook.Worksheets.Add("チェックリスト");

        // ヘッダー
        SetHeaderCell(ws, 1, 1, "PC名", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 2, "全体判定", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 3, "#", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 4, "モジュール名", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 5, "カテゴリ", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 6, "結果", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 7, "実行時刻", ChecklistHeaderBg);
        SetHeaderCell(ws, 1, 8, "メッセージ", ChecklistHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!results.TryGetValue(pc.PcName, out var result)) continue;

            if (result.ModuleItems.Count == 0)
            {
                // モジュール結果がない場合でも全体判定だけ出力
                ws.Cell(row, 1).Value = pc.PcName;
                ws.Cell(row, 2).Value = result.OverallStatus;
                ApplyStatusColor(ws.Cell(row, 2), result.OverallStatus);
                row++;
                continue;
            }

            foreach (var module in result.ModuleItems)
            {
                ws.Cell(row, 1).Value = pc.PcName;
                ws.Cell(row, 2).Value = result.OverallStatus;
                ApplyStatusColor(ws.Cell(row, 2), result.OverallStatus);
                ws.Cell(row, 3).Value = module.Order;
                ws.Cell(row, 4).Value = module.ModuleName;
                ws.Cell(row, 5).Value = module.Category;
                var statusCell = ws.Cell(row, 6);
                statusCell.Value = module.Status;
                ApplyStatusColor(statusCell, module.Status);
                ws.Cell(row, 7).Value = module.Time;
                ws.Cell(row, 8).Value = module.Message;
                row++;
            }
        }

        ApplyStyles(ws, row - 2, 8);
    }

    // ================================================================
    // Sheet 4: 実行履歴
    // ================================================================

    private static void WriteHistorySheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<string, IReadOnlyList<ExportHistoryEntry>> histories)
    {
        var ws = workbook.Worksheets.Add("実行履歴");

        // ヘッダー
        SetHeaderCell(ws, 1, 1, "PC名", HistoryHeaderBg);
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
            if (!histories.TryGetValue(pc.PcName, out var entries)) continue;

            foreach (var entry in entries)
            {
                ws.Cell(row, 1).Value = pc.PcName;
                ws.Cell(row, 2).Value = entry.Timestamp;
                ws.Cell(row, 3).Value = entry.ModuleName;
                ws.Cell(row, 4).Value = entry.Category;
                var statusCell = ws.Cell(row, 5);
                statusCell.Value = entry.Status;
                ApplyStatusColor(statusCell, entry.Status);
                ws.Cell(row, 6).Value = entry.Message;
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

    private static void WriteBaselineSheet(
        IXLWorkbook workbook,
        IReadOnlyList<PcEvidence> pcEvidences,
        IReadOnlyDictionary<string, BaselineComparisonReport> reports)
    {
        var ws = workbook.Worksheets.Add("ベースライン突合");

        // ヘッダー
        SetHeaderCell(ws, 1, 1, "PC名", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 2, "モジュール名", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 3, "カテゴリ", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 4, "期待ステータス", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 5, "実測ステータス", BaselineHeaderBg);
        SetHeaderCell(ws, 1, 6, "判定", BaselineHeaderBg);

        var row = 2;
        foreach (var pc in pcEvidences)
        {
            if (!reports.TryGetValue(pc.PcName, out var report)) continue;

            foreach (var item in report.Items)
            {
                ws.Cell(row, 1).Value = pc.PcName;
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
        if (upper is "OK" or "SUCCESS" or "MATCH")
        {
            cell.Style.Font.FontColor = MatchFont;
        }
        else if (upper is "NG" or "ERROR" or "MISMATCH" or "FAIL")
        {
            cell.Style.Font.FontColor = MismatchFont;
            cell.Style.Font.Bold = true;
        }
        else if (upper is "SKIP" or "SKIPPED")
        {
            cell.Style.Font.FontColor = XLColor.Gray;
        }
    }

    private static void ApplyStyles(IXLWorksheet ws, int dataRowCount, int totalColumns)
    {
        if (dataRowCount == 0 || totalColumns == 0)
            return;

        var lastRow = dataRowCount + 1;

        // 全セルに罫線
        var dataRange = ws.Range(1, 1, lastRow, totalColumns);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // データ行のフォント
        var bodyRange = ws.Range(2, 1, lastRow, totalColumns);
        bodyRange.Style.Font.FontName = "Arial";
        bodyRange.Style.Font.FontSize = 10;

        // 列幅自動調整 (最大幅制限)
        for (var col = 1; col <= totalColumns; col++)
        {
            ws.Column(col).AdjustToContents();
            if (ws.Column(col).Width > EvidenceConstants.ExcelMaxColumnWidth)
                ws.Column(col).Width = EvidenceConstants.ExcelMaxColumnWidth;
        }

        // ヘッダー行を固定
        ws.SheetView.FreezeRows(1);

        // オートフィルタ
        ws.Range(1, 1, lastRow, totalColumns).SetAutoFilter();
    }
}
