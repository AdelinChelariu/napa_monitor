namespace NapaMonitor.Models;

public class MetricSnapshot
{
    public DateTime Timestamp { get; set; }
    public int ActiveConnections { get; set; }
    public int MaxConnections { get; set; }
    public double DatabaseSizeMb { get; set; }
    public long TransactionsCommited { get; set; }
    public long TransactionsRolledBack { get; set; }
    public double CacheHitRatio { get; set; }
    public List<SlowQuery> SlowQueries { get; set; } = new ();
}