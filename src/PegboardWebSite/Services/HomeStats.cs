namespace PegboardWebSite.Services;

/// <summary>
/// The three homepage "social proof" aggregate counts, held as raw totals.
/// Rendered on the homepage via <see cref="StatsFormatter"/> (honest round-DOWN + "+").
/// Persisted as a single row (id = 1) in the home_stats table
/// (see deploy/sql/home_stats.sql).
/// </summary>
public class HomeStats
{
    public long GamesPlayed { get; set; }
    public long SessionsRun { get; set; }
    public long PlayersRated { get; set; }

    /// <summary>"seed" | "live" - provenance of the current values.</summary>
    public string Source { get; set; } = "seed";

    /// <summary>UTC of the last successful live fetch, or null while still on seed values.</summary>
    public DateTime? FetchedAtUtc { get; set; }
}

/// <summary>
/// Seed / ultimate-fallback values. These mirror the figures the homepage displayed
/// before the weekly-bake feature (honest, current as of 2026-07). Used to (a) seed the
/// store on first run and (b) render the page if the store is unreachable, so the stats
/// never flicker to zero or blank.
/// </summary>
public static class HomeStatsDefaults
{
    public static HomeStats Seed() => new()
    {
        GamesPlayed = 33000,
        SessionsRun = 1100,
        PlayersRated = 2000,
        Source = "seed",
        FetchedAtUtc = null
    };
}
