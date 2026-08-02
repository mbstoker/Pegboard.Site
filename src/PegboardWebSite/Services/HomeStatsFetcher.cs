using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PegboardWebSite.Services;

/// <summary>
/// Fetches canonical aggregate counts from the PegboardWeb prod diagnostics API for the
/// weekly homepage-stats bake.
///
/// PARKED (2026-07): the diagnostics API does NOT yet expose an aggregate endpoint. Its
/// current key-gated surface (/api/clubs, /api/sessions?clubId=) is per-club / per-session
/// only. This fetcher therefore targets a *proposed* Platform-owned endpoint
/// (Diagnostics:StatsUrl, default {Diagnostics:BaseUrl}/api/stats) that must return the
/// canonical headline totals. Until that endpoint ships, StatsUrl is left blank / the
/// endpoint 404s, every fetch fails gracefully, and the last-known (seed) values are
/// preserved. See the Platform dependency note in the PR / feature report.
///
/// Canonical definition (Mike's ruling 2026-07-31, "Option A"): count REAL clubs only —
/// demo clubs (is_demo) and template clubs (is_template) EXCLUDED — across all-time, and
/// INCLUDE the migrated legacy history (all badminton run through Pegboard's lineage).
/// Do NOT exclude legacy-imported sessions: that would shrink the headline to ~a quarter and
/// misrepresent the real footprint. Metrics: gamesPlayed = ScoreRecorded events in real clubs;
/// sessionsRun = ClubSessionStarted in real clubs; playersRated = distinct members with a
/// MemberRatingInitialized in real clubs.
///
/// Expected response contract (Platform to implement, exclusions applied server-side):
///   { "gamesPlayed": &lt;long&gt;, "sessionsRun": &lt;long&gt;, "playersRated": &lt;long&gt; }
/// </summary>
public class HomeStatsFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<HomeStatsFetcher> _logger;

    public HomeStatsFetcher(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<HomeStatsFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Attempt a fetch. Returns fresh stats on success, or null on any failure/absence
    /// (caller keeps the last-known stored values). Never throws; never logs the API key.
    /// </summary>
    public async Task<HomeStats?> TryFetchAsync(CancellationToken ct)
    {
        // API key: prefer the VPS env var, fall back to config (test override). Never logged.
        var apiKey = Environment.GetEnvironmentVariable("PEGBOARD_DIAG_API_KEY");
        if (string.IsNullOrEmpty(apiKey)) apiKey = _config["Diagnostics:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Stats bake skipped: diagnostics API key (PEGBOARD_DIAG_API_KEY) is not set. Keeping last-known values.");
            return null;
        }

        var url = _config["Diagnostics:StatsUrl"];
        if (string.IsNullOrEmpty(url))
        {
            var baseUrl = _config["Diagnostics:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = "https://play.epegboard.com";
            url = baseUrl.TrimEnd('/') + "/api/stats";
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stats bake fetch returned HTTP {Status} from the diagnostics API. Keeping last-known values.", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<StatsResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dto is null || dto.GamesPlayed <= 0 || dto.SessionsRun <= 0 || dto.PlayersRated <= 0)
            {
                _logger.LogWarning("Stats bake fetch returned missing/zero values. Keeping last-known values.");
                return null;
            }

            return new HomeStats
            {
                GamesPlayed = dto.GamesPlayed,
                SessionsRun = dto.SessionsRun,
                PlayersRated = dto.PlayersRated,
                Source = "live",
                FetchedAtUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stats bake fetch failed. Keeping last-known values.");
            return null;
        }
    }

    private sealed class StatsResponse
    {
        [JsonPropertyName("gamesPlayed")] public long GamesPlayed { get; set; }
        [JsonPropertyName("sessionsRun")] public long SessionsRun { get; set; }
        [JsonPropertyName("playersRated")] public long PlayersRated { get; set; }
    }
}
