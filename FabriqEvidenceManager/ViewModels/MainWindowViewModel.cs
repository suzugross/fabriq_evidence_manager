using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FabriqEvidenceManager.Helpers;
using FabriqEvidenceManager.Models;
using FabriqEvidenceManager.Services.Interfaces;
using Microsoft.Win32;

namespace FabriqEvidenceManager.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。
/// 各 Service をDI経由で受け取り、UIバインディングとコマンドを提供する。
/// ビジネスロジックは Service に委譲し、ここには持たない。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IEvidenceDiscoveryService _discoveryService;
    private readonly IEvidenceParserService _parserService;
    private readonly IEvidenceCollectorService _collectorService;
    private readonly IExcelExportService _excelExportService;

    public MainWindowViewModel(
        IEvidenceDiscoveryService discoveryService,
        IEvidenceParserService parserService,
        IEvidenceCollectorService collectorService,
        IExcelExportService excelExportService)
    {
        _discoveryService = discoveryService;
        _parserService = parserService;
        _collectorService = collectorService;
        _excelExportService = excelExportService;
    }

    // ================================================================
    // Observable Properties
    // ================================================================

    /// <summary>evidence フォルダのパス</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadEvidenceCommand))]
    private string _evidenceRootPath = string.Empty;

    /// <summary>抽出されたPC情報のリスト</summary>
    public ObservableCollection<PcEvidence> TargetPCs { get; } = [];

    /// <summary>処理中フラグ (プログレスバー表示、ボタン無効化に使用)</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadEvidenceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportDeliveryCommand))]
    private bool _isLoading;

    /// <summary>ステータスバーに表示するメッセージ</summary>
    [ObservableProperty]
    private string _statusMessage = "evidence フォルダを選択してください。";

    // ================================================================
    // Commands
    // ================================================================

    /// <summary>フォルダ選択ダイアログを開き、EvidenceRootPath にセットして自動で読み込みを開始する。</summary>
    [RelayCommand]
    private async Task SelectEvidenceFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "evidence フォルダを選択",
        };

        if (!string.IsNullOrEmpty(EvidenceRootPath) && Directory.Exists(EvidenceRootPath))
            dialog.InitialDirectory = EvidenceRootPath;

        if (dialog.ShowDialog() == true)
        {
            EvidenceRootPath = dialog.FolderName;

            if (LoadEvidenceCommand.CanExecute(null))
                await LoadEvidenceCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// EvidenceRootPath を元に Discovery → Parse を実行し、TargetPCs にデータをロードする。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadEvidence))]
    private async Task LoadEvidenceAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        StatusMessage = "evidence を読み込み中...";
        TargetPCs.Clear();

        try
        {
            // 1. Discovery: PC一覧を構築
            var pcList = await _discoveryService.DiscoverAsync(EvidenceRootPath, cancellationToken);

            // 2. Parse: 各PCのログファイルからデータを抽出
            foreach (var pc in pcList)
            {
                _parserService.PopulateDetails(pc);
                TargetPCs.Add(pc);
            }

            if (TargetPCs.Count == 0)
            {
                StatusMessage = "PC情報が見つかりませんでした。";
                MessageBox.Show(
                    "指定されたフォルダからPC情報を検出できませんでした。\n\n"
                    + "evidence フォルダ直下に以下のサブフォルダが存在するか確認してください:\n"
                    + "  - pc_information\n"
                    + "  - auto_capture\n"
                    + "  - bitlocker\n"
                    + "  - checklist\n"
                    + "  - export_history",
                    "検出結果",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                StatusMessage = $"読み込み完了: {TargetPCs.Count} 台のPCを検出しました。";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "読み込みがキャンセルされました。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"読み込みエラー: {ex.Message}";
            MessageBox.Show(
                $"evidence の読み込み中にエラーが発生しました。\n\n{ex.Message}",
                "読み込みエラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoadEvidence() =>
        !IsLoading && !string.IsNullOrWhiteSpace(EvidenceRootPath) && Directory.Exists(EvidenceRootPath);

    /// <summary>
    /// 納品データ一括出力。
    /// 出力先フォルダを選択し、{YYYY_MM_DD}_fabriq_evi フォルダを作成して
    /// 各PCのエビデンスディレクトリとPC情報一覧表Excelを出力する。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteAction))]
    private async Task ExportDeliveryAsync(CancellationToken cancellationToken)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "納品データの出力先フォルダを選択",
        };

        if (dialog.ShowDialog() != true)
            return;

        // 納品用ディレクトリの作成は Service に委譲
        var deliveryPath = _collectorService.CreateDeliveryDirectory(dialog.FolderName);

        IsLoading = true;

        try
        {
            // 1. エビデンスフォルダ整理
            var total = TargetPCs.Count;
            var pcList = TargetPCs.ToList().AsReadOnly();
            var progress = new Progress<int>(percent =>
            {
                var current = (int)Math.Ceiling(percent / 100.0 * total);
                StatusMessage = $"フォルダ整理中... {current}/{total} 台完了 ({percent}%)";
            });

            var copyErrors = await _collectorService.CollectAsync(pcList, deliveryPath, progress, cancellationToken);

            // 2. Excel 台帳出力
            StatusMessage = "Excel 台帳を出力中...";
            var excelFileName = $"{DateTime.Now:yyyy_MM_dd_HHmmss}_{EvidenceConstants.ExcelFileNameSuffix}.xlsx";
            var excelPath = Path.Combine(deliveryPath, excelFileName);

            await _excelExportService.ExportAsync(pcList, excelPath, cancellationToken);

            // コピー失敗ファイルがあれば警告表示
            if (copyErrors.Count > 0)
            {
                var errorSummary = string.Join("\n", copyErrors.Count <= 10
                    ? copyErrors
                    : copyErrors.Take(10).Append($"... 他 {copyErrors.Count - 10} 件"));

                MessageBox.Show(
                    $"以下のファイルのコピーに失敗しました（スキップ済み）:\n\n{errorSummary}",
                    "コピー警告",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            StatusMessage = $"出力完了: {deliveryPath}";
            MessageBox.Show(
                $"納品データを出力しました。\n\n"
                + $"出力先: {deliveryPath}\n"
                + $"PC台数: {total} 台\n"
                + $"Excel:  {excelFileName}"
                + (copyErrors.Count > 0 ? $"\n\n※ {copyErrors.Count} 件のファイルコピーに失敗しました。" : ""),
                "出力完了",
                MessageBoxButton.OK,
                copyErrors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "出力がキャンセルされました。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"出力エラー: {ex.Message}";
            MessageBox.Show(
                $"納品データの出力中にエラーが発生しました。\n\n{ex.Message}",
                "出力エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanExecuteAction() => !IsLoading && TargetPCs.Count > 0;
}
