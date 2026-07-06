namespace PegboardWebSite.Services;

/// <summary>
/// Weekly "bake" of the homepage social-proof stats. On startup it ensures the store is
/// seeded, then on a weekly timer it fetches canonical aggregates from the prod diagnostics
/// API and writes them to the home_stats store. On any fetch failure the last-known stored
/// values are preserved (never zeroed / blanked). The homepage renders from the store, so it
/// never makes a per-request call to the app.
///
/// The interval is configurable (Stats:RefreshIntervalHours, default 168 = weekly) so the
/// refresh loop can be exercised on a short interval during verification.
/// </summary>
public class StatsRefreshService : BackgroundService
{
    private const double DefaultIntervalHours = 168.0; // one week

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<StatsRefreshService> _logger;

    public StatsRefreshService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<StatsRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Seed the store first so the homepage always has sane values, even before the first fetch.
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<HomeStatsRepository>();
            repo.EnsureSchemaAndSeed(HomeStatsDefaults.Seed());
        }

        var hours = DefaultIntervalHours;
        if (double.TryParse(_config["Stats:RefreshIntervalHours"], out var configured) && configured > 0)
            hours = configured;
        var interval = TimeSpan.FromHours(hours);

        // Small settle delay so the bake doesn't compete with app startup.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshOnce(stoppingToken);
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshOnce(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var fetcher = scope.ServiceProvider.GetRequiredService<HomeStatsFetcher>();
            var repo = scope.ServiceProvider.GetRequiredService<HomeStatsRepository>();

            var fetched = await fetcher.TryFetchAsync(ct);
            if (fetched is not null)
            {
                repo.Upsert(fetched);
                _logger.LogInformation(
                    "Homepage stats refreshed from diagnostics API (games={Games}, sessions={Sessions}, players={Players}).",
                    fetched.GamesPlayed, fetched.SessionsRun, fetched.PlayersRated);
            }
            else
            {
                _logger.LogInformation("Homepage stats bake: no new values this cycle; last-known values retained.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Homepage stats bake cycle failed; last-known values retained.");
        }
    }
}
