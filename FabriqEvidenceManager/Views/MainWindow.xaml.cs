using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FabriqEvidenceManager.ViewModels;

namespace FabriqEvidenceManager.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// DataGrid 行ダブルクリックで PC 詳細ウィンドウを開く。
    /// 双方向追従ではなくスナップショット (起動時の SelectedPc + 関連レポート) を
    /// PcDetailViewModel に渡すため、複数の異なる PC をそれぞれ別ウィンドウで開ける。
    /// </summary>
    private void OnPcRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || row.Item is null) return;
        if (DataContext is not MainWindowViewModel vm || vm.SelectedPc is null) return;

        var detailVm = new PcDetailViewModel(
            pc: vm.SelectedPc,
            checkOptions: vm.CheckOptions,
            isHostlistLoaded: vm.IsHostlistLoaded,
            isBaselineLoaded: vm.IsBaselineLoaded,
            verification: vm.CurrentVerificationReport,
            checklist: vm.CurrentChecklistResult,
            history: vm.CurrentExportHistory,
            baseline: vm.CurrentBaselineReport,
            memoService: vm.MemoService);

        var detailWindow = new PcDetailWindow(detailVm) { Owner = this };
        detailWindow.Show();
    }

    /// <summary>
    /// 設定ボタンクリックで SettingsWindow を modeless で開く。
    /// 変更は即時反映 (OK/Cancel ではなく閉じるのみ)。
    /// </summary>
    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var settingsVm = vm.CreateSettingsViewModel();
        var settingsWindow = new SettingsWindow(settingsVm) { Owner = this };
        settingsWindow.Show();
    }
}
