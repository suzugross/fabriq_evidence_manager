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
        SourceInitialized += OnSourceInitializedClampSize;
        Loaded += OnLoadedClampPosition;
    }

    /// <summary>
    /// XAML 既定 Height=900 が画面 WorkArea より大きいケース (低解像度ノート / 高 DPI 環境)
    /// で CenterOwner の中心計算が画面外に出るのを未然に防ぐため、
    /// HWND 作成直後に Width/Height をクランプする。
    /// このタイミングで縮めれば後続の CenterOwner 配置が正しい中心になる。
    /// </summary>
    private void OnSourceInitializedClampSize(object? sender, System.EventArgs e)
    {
        var area = SystemParameters.WorkArea;
        if (Height > area.Height) Height = area.Height;
        if (Width  > area.Width)  Width  = area.Width;
    }

    /// <summary>
    /// CenterOwner 配置後の最終位置がそれでも WorkArea からはみ出した場合
    /// (オーナーウィンドウがマルチモニタ端にある等) に Top/Left を強制クランプ。
    /// タイトルバーが画面外に出てドラッグ移動できなくなる症状の防止。
    /// </summary>
    private void OnLoadedClampPosition(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        if (Top  < area.Top)              Top  = area.Top;
        if (Left < area.Left)             Left = area.Left;
        if (Top  + Height > area.Bottom)  Top  = area.Bottom - Height;
        if (Left + Width  > area.Right)   Left = area.Right  - Width;
    }
}
