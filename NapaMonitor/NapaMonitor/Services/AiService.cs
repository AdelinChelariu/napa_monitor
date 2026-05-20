using System.Net.Http;
using System.Text;
using System.Text.Json;
using NapaMonitor.Models;

namespace NapaMonitor.Services;

public class AiService
{
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model = "llama-3.1-8b-instant";

    public AiService(string apiKey)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<string> AnalyzeMetricsAsync(List<MetricSnapshot> snapshots)
    {
        var latest = snapshots.First();
        var prompt = BuildPrompt(latest, snapshots);

        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are a PostgreSQL database monitoring assistant. Be concise and practical."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            max_tokens = 1024,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(ApiUrl, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            var message = error.GetProperty("message").GetString();
            return $"Groq API error: {message}";
        }

        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? "No response received.";
    }

    private string BuildPrompt(MetricSnapshot latest, List<MetricSnapshot> history)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following PostgreSQL metrics and provide:");
        sb.AppendLine("1. A brief health summary");
        sb.AppendLine("2. Any concerns or anomalies");
        sb.AppendLine("3. Specific optimization suggestions if needed\n");

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