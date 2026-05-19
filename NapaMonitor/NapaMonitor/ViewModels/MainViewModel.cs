using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NapaMonitor.Models;
using NapaMonitor.Services;

namespace NapaMonitor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService;
    private readonly StorageService _storageService;
    private readonly AiService _aiService;
    private System.Timers.Timer? _timer;

    // Properties
    private MetricSnapshot? _latestSnapshot;
    private string _aiAnalysis = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isCollecting;
    private int _intervalSeconds = 30;

    public MetricSnapshot? LatestSnapshot
    {
        get => _latestSnapshot;
        set { _latestSnapshot = value; OnPropertyChanged(); }
    }

    public string AiAnalysis
    {
        get => _aiAnalysis;
        set { _aiAnalysis = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsCollecting
    {
        get => _isCollecting;
        set { _isCollecting = value; OnPropertyChanged(); }
    }

    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set { _intervalSeconds = value; OnPropertyChanged(); }
    }

    public ObservableCollection<MetricSnapshot> SnapshotHistory { get; } = new();

    // Constructor
    public MainViewModel(string connectionString, string geminiApiKey)
    {
        _databaseService = new DatabaseService(connectionString);
        _storageService = new StorageService();
        _aiService = new AiService(geminiApiKey);
    }

    // Commands
    public async Task StartMonitoringAsync()
    {
        if (IsCollecting) return;

        IsCollecting = true;
        StatusMessage = "Monitoring started...";

        await CollectAndSaveAsync();

        _timer = new System.Timers.Timer(IntervalSeconds * 1000);
        _timer.Elapsed += async (_, _) => await CollectAndSaveAsync();
        _timer.Start();
    }

    public void StopMonitoring()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        IsCollecting = false;
        StatusMessage = "Monitoring stopped.";
    }

    public async Task AnalyzeWithAiAsync()
    {
        if (SnapshotHistory.Count == 0)
        {
            StatusMessage = "No data to analyze yet.";
            return;
        }

        StatusMessage = "Asking Gemini...";
        AiAnalysis = "Loading...";

        try
        {
            var snapshots = SnapshotHistory.ToList();
            AiAnalysis = await _aiService.AnalyzeMetricsAsync(snapshots);
            StatusMessage = "AI analysis complete.";
        }
        catch (Exception ex)
        {
            AiAnalysis = $"Error: {ex.Message}";
            StatusMessage = "AI analysis failed.";
        }
    }

    private async Task CollectAndSaveAsync()
    {
        try
        {
            StatusMessage = "Collecting metrics...";
            var snapshot = await _databaseService.CollectMetricsAsync();

            await _storageService.SaveSnapshotAsync(snapshot);

            App.Current.Dispatcher.Invoke(() =>
            {
                SnapshotHistory.Insert(0, snapshot);
                LatestSnapshot = snapshot;

                if (SnapshotHistory.Count > 50)
                    SnapshotHistory.RemoveAt(SnapshotHistory.Count - 1);
            });

            StatusMessage = $"Last updated: {snapshot.Timestamp:HH:mm:ss} UTC";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}