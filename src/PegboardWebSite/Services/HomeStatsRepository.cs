using Npgsql;

namespace PegboardWebSite.Services;

/// <summary>
/// Reads / writes the single-row home_stats aggregate store (see deploy/sql/home_stats.sql).
/// All DB access is best-effort and swallowed (log + null/no-op) - a stats-store failure
/// must never surface as a 5xx on the homepage; the caller falls back to seed defaults
/// (same convention as <see cref="TrackedRequestRepository"/>, after the 2026-05-31 outage).
/// </summary>
public class HomeStatsRepository
{
    private const int RowId = 1;

    private readonly IConfiguration _config;
    private readonly ILogger<HomeStatsRepository> _logger;

    public HomeStatsRepository(IConfiguration config, ILogger<HomeStatsRepository> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string ConnectionString => _config.GetConnectionString("PegboardDb")!;

    /// <summary>Read the stored stats, or null if unavailable (caller falls back to seed defaults).</summary>
    public HomeStats? Read()
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT games_played, sessions_run, players_rated, source, fetched_at_utc " +
                "FROM home_stats WHERE id = @id",
                connection);
            cmd.Parameters.AddWithValue("@id", RowId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new HomeStats
                {
                    GamesPlayed = reader.GetInt64(0),
                    SessionsRun = reader.GetInt64(1),
                    PlayersRated = reader.GetInt64(2),
                    Source = reader.IsDBNull(3) ? "seed" : reader.GetString(3),
                    FetchedAtUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HomeStats Read failed - homepage will fall back to seed defaults.");
        }
        return null;
    }

    /// <summary>
    /// Ensure the table exists and a seed row is present. Idempotent and additive
    /// (CREATE TABLE IF NOT EXISTS + INSERT ... ON CONFLICT DO NOTHING), so it never
    /// overwrites a live value. Mirrors deploy/sql/home_stats.sql to self-bootstrap
    /// local/dev DBs; harmless in prod where the script has already been applied.
    /// </summary>
    public void EnsureSchemaAndSeed(HomeStats seed)
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using (var ddl = new NpgsqlCommand(
                "CREATE TABLE IF NOT EXISTS home_stats (" +
                "  id             SMALLINT    PRIMARY KEY," +
                "  games_played   BIGINT      NOT NULL," +
                "  sessions_run   BIGINT      NOT NULL," +
                "  players_rated  BIGINT      NOT NULL," +
                "  source         TEXT        NOT NULL DEFAULT 'seed'," +
                "  fetched_at_utc TIMESTAMPTZ NULL," +
                "  updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now())",
                connection))
            {
                ddl.ExecuteNonQuery();
            }

            using var cmd = new NpgsqlCommand(
                "INSERT INTO home_stats (id, games_played, sessions_run, players_rated, source, updated_at_utc) " +
                "VALUES (@id, @games, @sessions, @players, @source, now()) " +
                "ON CONFLICT (id) DO NOTHING",
                connection);
            cmd.Parameters.AddWithValue("@id", RowId);
            cmd.Parameters.AddWithValue("@games", seed.GamesPlayed);
            cmd.Parameters.AddWithValue("@sessions", seed.SessionsRun);
            cmd.Parameters.AddWithValue("@players", seed.PlayersRated);
            cmd.Parameters.AddWithValue("@source", seed.Source);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HomeStats EnsureSchemaAndSeed failed - the weekly bake will retry; homepage falls back to seed defaults.");
        }
    }

    /// <summary>Overwrite the stored values (called by the weekly bake on a successful fetch).</summary>
    public void Upsert(HomeStats stats)
    {
        try
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(
                "INSERT INTO home_stats (id, games_played, sessions_run, players_rated, source, fetched_at_utc, updated_at_utc) " +
                "VALUES (@id, @games, @sessions, @players, @source, @fetched, now()) " +
                "ON CONFLICT (id) DO UPDATE SET " +
                "  games_played   = EXCLUDED.games_played, " +
                "  sessions_run   = EXCLUDED.sessions_run, " +
                "  players_rated  = EXCLUDED.players_rated, " +
                "  source         = EXCLUDED.source, " +
                "  fetched_at_utc = EXCLUDED.fetched_at_utc, " +
                "  updated_at_utc = now()",
                connection);
            cmd.Parameters.AddWithValue("@id", RowId);
            cmd.Parameters.AddWithValue("@games", stats.GamesPlayed);
            cmd.Parameters.AddWithValue("@sessions", stats.SessionsRun);
            cmd.Parameters.AddWithValue("@players", stats.PlayersRated);
            cmd.Parameters.AddWithValue("@source", stats.Source);
            cmd.Parameters.AddWithValue("@fetched", (object?)stats.FetchedAtUtc ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HomeStats Upsert failed - keeping previous stored values.");
        }
    }
}
