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
/// 列構成 (動的):
///   PC名, シリアル番号,
///   NIC1_MAC, NIC1_アダプタ, NIC1_接続名, NIC2_MAC, NIC2_アダプタ, NIC2_接続名, ...
///   Cドライブ_キーID, Cドライブ_回復キー, Dドライブ_キーID, Dドライブ_回復キー, ...
/// </summary>
public sealed class ExcelExportService : IExcelExportService
{
    // 固定列数 (PC名, シリアル番号, 収集日)
    private const int FixedColumnCount = 3;

    // NIC1列あたりのサブ列数 (MAC, アダプタ, 接続名)
    private const int NicSubColumnCount = 3;

    // ドライブ1つあたりのサブ列数 (キーID, 回復キー)
    private const int DriveSubColumnCount = 2;

    // ヘッダー背景色
    private static readonly XLColor BaseHeaderBg = XLColor.FromHtml("#4472C4");   // 青: 基本情報
    private static readonly XLColor NicHeaderBg = XLColor.FromHtml("#548235");    // 緑: NIC
    private static readonly XLColor KeyHeaderBg = XLColor.FromHtml("#BF8F00");    // 黄: 回復キー
    private static readonly XLColor HeaderFontColor = XLColor.White;

    public Task ExportAsync(
        IReadOnlyList<PcEvidence> pcEvidences,
        string outputFilePath,
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
        var ws = workbook.Worksheets.Add(EvidenceConstants.ExcelSheetName);

        // ---- ヘッダー行 ----
        WriteHeader(ws, maxNicCount, allDriveLetters);

        // ---- データ行 ----
        for (var i = 0; i < pcEvidences.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteDataRow(ws, row: i + 2, pcEvidences[i], maxNicCount, allDriveLetters);
        }

        // ---- 書式設定 ----
        ApplyStyles(ws, pcEvidences.Count, totalColumns);

        workbook.SaveAs(outputFilePath);
        return Task.CompletedTask;
    }

    /// <summary>ヘッダー行を動的に構築する。</summary>
    private static void WriteHeader(IXLWorksheet ws, int maxNicCount, List<string> driveLetters)
    {
        var col = 1;

        // 固定列
        SetHeaderCell(ws, 1, col++, "PC名", BaseHeaderBg);
        SetHeaderCell(ws, 1, col++, "シリアル番号", BaseHeaderBg);
        SetHeaderCell(ws, 1, col++, "収集日", BaseHeaderBg);

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

    /// <summary>ヘッダーセルに値と書式を設定する。</summary>
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

    /// <summary>1PCのデータを1行に書き込む。</summary>
    private static void WriteDataRow(
        IXLWorksheet ws, int row, PcEvidence pc,
        int maxNicCount, List<string> driveLetters)
    {
        var col = 1;

        // PC名, シリアル番号, 収集日
        ws.Cell(row, col++).Value = pc.PcName;
        ws.Cell(row, col++).Value = pc.SerialNumber;
        ws.Cell(row, col++).Value = pc.CollectionDate;

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
                // このPCにはこのNIC番号のデータがない → 空セル
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

    /// <summary>罫線・列幅自動調整・フリーズペインなどの書式を適用する。</summary>
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
