using System.Windows;
using NapaMonitor.ViewModels;

namespace NapaMonitor.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.StartMonitoringAsync();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StopMonitoring();
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.AnalyzeWithAiAsync();
    }
}