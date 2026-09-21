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
/// Seed / ultimate-fallback values. Used to (a) seed the store on first run and (b) render
/// the page if the store is unreachable, so the stats never flicker to zero or blank.
///
/// Refreshed 2026-09-20 from prod (pegboard.prod) on the "Option A" definition Mike chose:
/// real clubs only (demo + template clubs EXCLUDED) across all-time, INCLUDING the migrated
/// legacy history — i.e. all badminton run through Pegboard's lineage. Raw totals;
/// StatsFormatter rounds DOWN, so these render 44,000+ / 1,600+ / 4,100+.
/// (Previous refreshes: 33k/1.1k/2k day-one seed, then 36440/1276/2600 on 2026-07-31.)
///
/// These are still refreshed BY HAND, seven weeks apart, because the weekly bake has never
/// run: the /api/stats endpoint it fetches from was never built — see HomeStatsFetcher.
/// Note this seed only applies to a cold store (ON CONFLICT DO NOTHING); the live figures
/// come from the home_stats row, which a prod UPDATE must set separately.
/// </summary>
public static class HomeStatsDefaults
{
    public static HomeStats Seed() => new()
    {
        GamesPlayed = 44705,
        SessionsRun = 1617,
        PlayersRated = 4193,
        Source = "seed",
        FetchedAtUtc = null
    };
}
