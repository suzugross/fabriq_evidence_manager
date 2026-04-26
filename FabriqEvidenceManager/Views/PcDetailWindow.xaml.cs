using System.Windows;
using FabriqEvidenceManager.ViewModels;

namespace FabriqEvidenceManager.Views;

/// <summary>
/// PC 個別詳細ウィンドウ。MainWindow から DataGrid 行のダブルクリックで起動される。
/// 自身の <see cref="PcDetailViewModel"/> を持ち、対象 PcEvidence を独立保持するため
/// 異なる PC を別ウィンドウで並べて比較できる。
/// </summary>
public partial class PcDetailWindow : Window
{
    public PcDetailWindow(PcDetailViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
