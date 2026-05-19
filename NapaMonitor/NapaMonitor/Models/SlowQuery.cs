namespace NapaMonitor.Models;

public class SlowQuery
{
    public string Query { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public string State { get; set; } = string.Empty;
}