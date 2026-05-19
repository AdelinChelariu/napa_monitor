using System.IO;
using System.Text.Json;
using NapaMonitor.Models;

namespace NapaMonitor.Services;

public class StorageService
{
    private readonly string _dataFolder;

    public StorageService()
    {
        _dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(_dataFolder);
    }

    public async Task SaveSnapshotAsync(MetricSnapshot snapshot)
    {
        var fileName = $"metrics_{DateTime.UtcNow:yyyy-MM-dd}.json";
        var filePath = Path.Combine(_dataFolder, fileName);

        var snapshots = await LoadAllSnapshotsFromFileAsync(filePath);
        snapshots.Add(snapshot);

        var json = JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<MetricSnapshot>> LoadRecentSnapshotsAsync(int count = 50)
    {
        var files = Directory.GetFiles(_dataFolder, "metrics_*.json")
            .OrderByDescending(f => f)
            .Take(2)
            .ToList();

        var allSnapshots = new List<MetricSnapshot>();
        foreach (var file in files)
        {
            var snapshots = await LoadAllSnapshotsFromFileAsync(file);
            allSnapshots.AddRange(snapshots);
        }

        return allSnapshots
            .OrderByDescending(s => s.Timestamp)
            .Take(count)
            .ToList();
    }

    private async Task<List<MetricSnapshot>> LoadAllSnapshotsFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return new List<MetricSnapshot>();

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<List<MetricSnapshot>>(json) ?? new List<MetricSnapshot>();
    }
}