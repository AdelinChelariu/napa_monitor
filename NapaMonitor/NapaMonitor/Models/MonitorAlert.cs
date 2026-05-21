namespace NapaMonitor.Models;

public class MonitorAlert
{
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
}

public enum AlertSeverity
{
    Warning,
    Critical
}