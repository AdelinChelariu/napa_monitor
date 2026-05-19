using Npgsql;
using NapaMonitor.Models;

namespace NapaMonitor.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<MetricSnapshot> CollectMetricsAsync()
    {
        var snapshot = new MetricSnapshot
        {
            Timestamp = DateTime.UtcNow
        };
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        snapshot.ActiveConnections = await GetActiveConnectionsAsync(connection);
        snapshot.MaxConnections = await GetMaxConnectionsAsync(connection);
        snapshot.DatabaseSizeMb = await GetDatabaseSizeMbAsync(connection);
        snapshot.CacheHitRatio = await GetCacheHitRatioAsync(connection);
        snapshot.SlowQueries = await GetSlowQueriesAsync(connection);

        return snapshot;
    }
    
    private async Task<int> GetActiveConnectionsAsync(NpgsqlConnection conn)
    {
        const string sql = "SELECT count(*) FROM pg_stat_activity WHERE state = 'active';";
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
    
    private async Task<int> GetMaxConnectionsAsync(NpgsqlConnection conn)
    {
        const string sql = "SHOW max_connections;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    
    private async Task<double> GetDatabaseSizeMbAsync(NpgsqlConnection conn)
    {
        const string sql = "SELECT pg_database_size(current_database()) / 1024.0 / 1024.0;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToDouble(result);
    }
    
    private async Task<double> GetCacheHitRatioAsync(NpgsqlConnection conn)
    {
        const string sql = @"
            SELECT 
                CASE WHEN (blks_hit + blks_read) = 0 THEN 0
                ELSE round((blks_hit::numeric / (blks_hit + blks_read)) * 100, 2)
                END
            FROM pg_stat_database
            WHERE datname = current_database();";
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToDouble(result);
    }
    
    private async Task<List<SlowQuery>> GetSlowQueriesAsync(NpgsqlConnection conn)
    {
        const string sql = @"
            SELECT query, extract(epoch from now() - query_start) as duration, state
            FROM pg_stat_activity
            WHERE state != 'idle'
              AND query_start IS NOT NULL
              AND extract(epoch from now() - query_start) > 1
            ORDER BY duration DESC
            LIMIT 5;";

        var slowQueries = new List<SlowQuery>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            slowQueries.Add(new SlowQuery
            {
                Query = reader.GetString(0),
                DurationSeconds = reader.GetDouble(1),
                State = reader.GetString(2)
            });
        }

        return slowQueries;
    }
}