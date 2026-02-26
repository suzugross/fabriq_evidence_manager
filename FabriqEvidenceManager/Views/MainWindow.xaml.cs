using System.Windows;
using FabriqEvidenceManager.ViewModels;

namespace FabriqEvidenceManager.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
