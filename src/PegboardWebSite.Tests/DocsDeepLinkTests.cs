using System.Text.RegularExpressions;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// Guards the routes from the marketing site into the customer docs (ado:2396).
///
/// Most of docs.epegboard.com sat in "Crawled - currently not indexed": a young subdomain whose
/// pages had few strong links in. Each www page that covers the same ground as a docs page must
/// link to it from its own content, with descriptive anchor text in a sentence. The layout's nav
/// and footer link only the docs root, so they do not count, and the assertion is scoped to
/// &lt;main&gt;. Targets are the final URLs after the docs consolidation, never a page that now
/// redirects (the old /insights/&lt;report&gt;/ pages and /how-to/run-a-competition/).
/// </summary>
public sealed class DocsDeepLinkTests : IClassFixture<TrackApiFactory>
{
    private const string Docs = "https://docs.epegboard.com";

    private readonly TrackApiFactory _factory;

    public DocsDeepLinkTests(TrackApiFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// The page's own content, so a nav or footer link cannot satisfy the assertion. The marketing
    /// layout renders a bare &lt;main&gt;; the older desktop pages use one with attributes.
    /// </summary>
    private static string MainContent(string html)
    {
        var m = Regex.Match(html, "<main[^>]*>(.*)</main>", RegexOptions.Singleline);
        Assert.True(m.Success, "No <main> element in the rendered page");
        return m.Groups[1].Value;
    }

    [Theory]
    [InlineData("/features/club-nights", "/club-nights/kiosk-mode/")]
    [InlineData("/features/club-nights", "/club-nights/sessions/")]
    [InlineData("/features/club-nights", "/club-nights/attendee-stats/")]
    [InlineData("/features/club-management", "/insights/reports/")]
    [InlineData("/features/club-management", "/insights/reports/#attendance-trends")]
    [InlineData("/features/club-management", "/club-nights/members/")]
    [InlineData("/features/club-management", "/club-nights/payments/")]
    [InlineData("/features/club-management", "/how-to/import-your-roster/")]
    [InlineData("/features/competitions", "/how-to/set-up-a-tournament/")]
    [InlineData("/for-players", "/for-players/")]
    [InlineData("/for-players", "/ratings/how-a-rating-change-is-calculated/")]
    [InlineData("/faq", "/club-nights/kiosk-mode/")]
    [InlineData("/faq", "/how-to/import-your-roster/")]
    [InlineData("/download", "/how-to/move-from-desktop/")]
    [InlineData("/featuresdesktop", "/how-to/move-from-desktop/")]
    [InlineData("/desktopreleasenotes", "/how-to/move-from-desktop/")]
    [InlineData("/guides/collecting-club-subs", "/club-nights/payments/")]
    [InlineData("/guides/setting-club-subs", "/club-nights/payments/")]
    [InlineData("/guides/paper-pegboard-to-app", "/how-to/import-your-roster/")]
    [InlineData("/guides/paper-pegboard-to-app", "/how-to/move-from-desktop/")]
    [InlineData("/guides/first-badminton-club-night", "/for-players/")]
    [InlineData("/guides/how-to-run-a-badminton-club-night", "/tutorials/first-club-night/")]
    [InlineData("/guides/game-variety-stop-same-players", "/club-nights/special-requests/")]
    [InlineData("/guides/mixed-ability-badminton-club-night", "/club-nights/special-requests/")]
    [InlineData("/guides/fair-court-time-busy-club-night", "/club-nights/attendee-stats/")]
    [InlineData("/guides/badminton-club-rating-systems", "/ratings/how-a-rating-change-is-calculated/")]
    [InlineData("/guides/picking-league-teams-club-data", "/league-teams/")]
    [InlineData("/guides/picking-league-teams-club-data", "/insights/reports/#partnerships")]
    [InlineData("/guides/how-to-run-a-local-badminton-league", "/league-teams/")]
    [InlineData("/guides/rolling-out-epegboard-to-your-club", "/tutorials/rolling-out-to-your-club/")]
    [InlineData("/guides/how-to-run-handicap-doubles-tournament", "/how-to/set-up-a-tournament/")]
    public async Task Pages_link_to_the_docs_page_that_covers_the_same_ground(string source, string docsPath)
    {
        var content = MainContent(await Client().GetStringAsync(source));

        Assert.Contains($"href=\"{Docs}{docsPath}\"", content);
    }

    /// <summary>
    /// The docs consolidation turned these into redirects. A link to one costs a hop and points
    /// search engines at a URL that is no longer a page, so no page may link to them.
    /// </summary>
    [Theory]
    [MemberData(nameof(PagesThatLinkTheDocs))]
    public async Task Pages_do_not_link_to_docs_pages_that_now_redirect(string source)
    {
        var html = await Client().GetStringAsync(source);

        // No trailing slash required, so the slashless form of a retired URL is caught too.
        Assert.DoesNotMatch(
            new Regex(@"docs\.epegboard\.com/(how-to/run-a-competition|insights/(attendance-over-time|session-summary|attendance-trends|member-engagement|match-quality|partnerships|payment-summary|court-utilisation|most-improved))\b"),
            html);
    }

    /// <summary>Every page that links into the docs: the 21 sources above, plus the category page.</summary>
    public static TheoryData<string> PagesThatLinkTheDocs() => new()
    {
        "/features/club-nights",
        "/features/club-management",
        "/features/competitions",
        "/for-players",
        "/faq",
        "/download",
        "/featuresdesktop",
        "/desktopreleasenotes",
        "/guides/collecting-club-subs",
        "/guides/setting-club-subs",
        "/guides/paper-pegboard-to-app",
        "/guides/first-badminton-club-night",
        "/guides/how-to-run-a-badminton-club-night",
        "/guides/game-variety-stop-same-players",
        "/guides/mixed-ability-badminton-club-night",
        "/guides/fair-court-time-busy-club-night",
        "/guides/badminton-club-rating-systems",
        "/guides/picking-league-teams-club-data",
        "/guides/how-to-run-a-local-badminton-league",
        "/guides/rolling-out-epegboard-to-your-club",
        "/guides/how-to-run-handicap-doubles-tournament",
        "/badminton-club-management-software",
    };
}
