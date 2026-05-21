using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NapaMonitor.Models;
using NapaMonitor.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

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
    
    public ObservableCollection<MonitorAlert> ActiveAlerts { get; } = new();
    
    public ISeries[] ConnectionsSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] CacheHitSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] DbSizeSeries { get; set; } = Array.Empty<ISeries>();

    public Axis[] TimeAxis { get; set; } = { new Axis { Labels = new List<string>() } };

    // Constructor
    public MainViewModel(string connectionString)
    {
        _databaseService = new DatabaseService(connectionString);
        _storageService = new StorageService();
        _aiService = new AiService();
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

        StatusMessage = "Asking AI...";
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
                UpdateCharts();
                CheckAlerts(snapshot);

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
    
    private void UpdateCharts()
    {
        var ordered = SnapshotHistory.OrderBy(s => s.Timestamp).ToList();
        var labels = ordered.Select(s => s.Timestamp.ToString("HH:mm:ss")).ToList();

        ConnectionsSeries = new ISeries[]
        {
            new LineSeries<int>
            {
                Values = ordered.Select(s => s.ActiveConnections).ToList(),
                Name = "Active Connections",
                Stroke = new SolidColorPaint(SKColor.Parse("#4A9EFF")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#4A9EFF")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#4A9EFF")),
                GeometrySize = 6,
                Fill = new SolidColorPaint(SKColor.Parse("#1A4A7F")) { StrokeThickness = 0 }
            }
        };

        CacheHitSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = ordered.Select(s => s.CacheHitRatio).ToList(),
                Name = "Cache Hit Ratio %",
                Stroke = new SolidColorPaint(SKColor.Parse("#4CAF50")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                GeometrySize = 6,
                Fill = new SolidColorPaint(SKColor.Parse("#1A4F1A")) { StrokeThickness = 0 }
            }
        };

        DbSizeSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = ordered.Select(s => s.DatabaseSizeMb).ToList(),
                Name = "Database Size (MB)",
                Stroke = new SolidColorPaint(SKColor.Parse("#FFC107")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#FFC107")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#FFC107")),
                GeometrySize = 6,
                Fill = new SolidColorPaint(SKColor.Parse("#4F3A00")) { StrokeThickness = 0 }
            }
        };

        TimeAxis = new Axis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#A0A0B0")),
                TextSize = 10
            }
        };

        OnPropertyChanged(nameof(ConnectionsSeries));
        OnPropertyChanged(nameof(CacheHitSeries));
        OnPropertyChanged(nameof(DbSizeSeries));
        OnPropertyChanged(nameof(TimeAxis));
    }
    
    private void CheckAlerts(MetricSnapshot snapshot)
    {
        ActiveAlerts.Clear();

        if (snapshot.MaxConnections > 0)
        {
            var connectionUsage = (double)snapshot.ActiveConnections / snapshot.MaxConnections * 100;
            if (connectionUsage >= 80)
                ActiveAlerts.Add(new MonitorAlert
                {
                    Message = $"High connection usage: {connectionUsage:F0}% of max connections used",
                    Severity = AlertSeverity.Critical
                });
            else if (connectionUsage >= 50)
                ActiveAlerts.Add(new MonitorAlert
                {
                    Message = $"Moderate connection usage: {connectionUsage:F0}% of max connections used",
                    Severity = AlertSeverity.Warning
                });
        }

        if (snapshot.CacheHitRatio < 90 && snapshot.CacheHitRatio > 0)
            ActiveAlerts.Add(new MonitorAlert
            {
                Message = $"Low cache hit ratio: {snapshot.CacheHitRatio}% (ideal: above 99%)",
                Severity = AlertSeverity.Critical
            });
        else if (snapshot.CacheHitRatio < 99 && snapshot.CacheHitRatio >= 90)
            ActiveAlerts.Add(new MonitorAlert
            {
                Message = $"Cache hit ratio below optimal: {snapshot.CacheHitRatio}% (ideal: above 99%)",
                Severity = AlertSeverity.Warning
            });

        if (snapshot.SlowQueries.Count > 0)
            ActiveAlerts.Add(new MonitorAlert
            {
                Message = $"{snapshot.SlowQueries.Count} slow query/queries detected (over 1 second)",
                Severity = AlertSeverity.Warning
            });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}