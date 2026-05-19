using System.Net.Http;
using System.Text;
using System.Text.Json;
using NapaMonitor.Models;

namespace NapaMonitor.Services;

public class AiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    public AiService(string apiKey)
    {
        _httpClient = new HttpClient();
        _apiKey = apiKey;
    }

    public async Task<string> AnalyzeMetricsAsync(List<MetricSnapshot> snapshots)
    {
        var latest = snapshots.First();
        var prompt = BuildPrompt(latest, snapshots);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{ApiUrl}?key={_apiKey}", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
                  .GetProperty("candidates")[0]
                  .GetProperty("content")
                  .GetProperty("parts")[0]
                  .GetProperty("text")
                  .GetString() ?? "No response received.";
    }

    private string BuildPrompt(MetricSnapshot latest, List<MetricSnapshot> history)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a PostgreSQL database monitoring assistant.");
        sb.AppendLine("Analyze the following metrics and provide:");
        sb.AppendLine("1. A brief health summary");
        sb.AppendLine("2. Any concerns or anomalies");
        sb.AppendLine("3. Specific optimization suggestions if needed");
        sb.AppendLine("Keep the response concise and practical.\n");

        sb.AppendLine("=== LATEST SNAPSHOT ===");
        sb.AppendLine($"Timestamp: {latest.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Active Connections: {latest.ActiveConnections} / {latest.MaxConnections}");
        sb.AppendLine($"Database Size: {latest.DatabaseSizeMb:F2} MB");
        sb.AppendLine($"Cache Hit Ratio: {latest.CacheHitRatio}%");

        if (latest.SlowQueries.Any())
        {
            sb.AppendLine($"\nSlow Queries ({latest.SlowQueries.Count}):");
            foreach (var q in latest.SlowQueries)
                sb.AppendLine($"  - [{q.DurationSeconds:F1}s] {q.Query[..Math.Min(100, q.Query.Length)]}...");
        }

        if (history.Count > 1)
        {
            sb.AppendLine("\n=== TREND (last snapshots) ===");
            sb.AppendLine($"Avg Connections: {history.Average(s => s.ActiveConnections):F1}");
            sb.AppendLine($"Avg Cache Hit Ratio: {history.Average(s => s.CacheHitRatio):F1}%");
        }

        return sb.ToString();
    }
}