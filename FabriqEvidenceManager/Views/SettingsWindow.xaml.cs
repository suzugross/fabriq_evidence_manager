using System.Windows;
using FabriqEvidenceManager.ViewModels;

namespace FabriqEvidenceManager.Views;

/// <summary>
/// 環境設定ダイアログ (modeless)。
/// メイン画面の「設定」ボタンから起動される。
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
