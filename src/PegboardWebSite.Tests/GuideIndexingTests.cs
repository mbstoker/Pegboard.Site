using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// Guards the guides Google had crawled and then left out of its index (ado:2397).
///
/// Six guides sat in "Crawled - currently not indexed". Measured against the rest of the estate
/// none of them duplicated another page's text, so each was kept and improved rather than merged:
/// related guides and the matching feature pages now link to them from their own content, the
/// structured data says when each was last revised, and the sitemap agrees with it. The layout's
/// nav and footer do not count as links, so link assertions are scoped to &lt;main&gt;.
/// </summary>
public sealed class GuideIndexingTests : IClassFixture<TrackApiFactory>
{
    private const string Revised = "2026-11-02";

    private readonly TrackApiFactory _factory;

    public GuideIndexingTests(TrackApiFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string MainContent(string html)
    {
        var m = Regex.Match(html, "<main[^>]*>(.*)</main>", RegexOptions.Singleline);
        Assert.True(m.Success, "No <main> element in the rendered page");
        return m.Groups[1].Value;
    }

    public static TheoryData<string> RevisedGuides() => new()
    {
        "/guides/how-to-start-a-badminton-club",
        "/guides/badminton-club-night-rotation",
        "/guides/badminton-club-night-formats-compared",
        "/guides/badminton-club-rating-systems",
        "/guides/game-variety-stop-same-players",
        "/guides/picking-league-teams-club-data",
    };

    /// <summary>Each revised guide with its first-publication date, which must never move.</summary>
    public static TheoryData<string, string> RevisedGuidesFirstPublished() => new()
    {
        { "/guides/how-to-start-a-badminton-club", "2026-07-05" },
        { "/guides/badminton-club-night-rotation", "2026-07-01" },
        { "/guides/badminton-club-night-formats-compared", "2026-07-05" },
        { "/guides/badminton-club-rating-systems", "2026-07-01" },
        { "/guides/game-variety-stop-same-players", "2026-07-01" },
        { "/guides/picking-league-teams-club-data", "2026-07-01" },
    };

    [Theory]
    // badminton-club-night-rotation
    [InlineData("/guides/managing-club-night-rotation", "/guides/badminton-club-night-rotation")]
    [InlineData("/guides/how-to-start-a-badminton-club", "/guides/badminton-club-night-rotation")]
    [InlineData("/features/club-nights", "/guides/badminton-club-night-rotation")]
    // badminton-club-night-formats-compared
    [InlineData("/guides/managing-club-night-rotation", "/guides/badminton-club-night-formats-compared")]
    [InlineData("/guides/how-to-run-a-badminton-round-robin", "/guides/badminton-club-night-formats-compared")]
    [InlineData("/guides/how-to-run-a-badminton-club-night", "/guides/badminton-club-night-formats-compared")]
    [InlineData("/guides/how-to-start-a-badminton-club", "/guides/badminton-club-night-formats-compared")]
    [InlineData("/guides/badminton-club-night-rotation", "/guides/badminton-club-night-formats-compared")]
    // game-variety-stop-same-players
    [InlineData("/guides/badminton-club-night-rotation", "/guides/game-variety-stop-same-players")]
    [InlineData("/guides/managing-club-night-rotation", "/guides/game-variety-stop-same-players")]
    [InlineData("/guides/mixed-ability-badminton-club-night", "/guides/game-variety-stop-same-players")]
    [InlineData("/guides/how-to-run-a-badminton-club-night", "/guides/game-variety-stop-same-players")]
    [InlineData("/guides/badminton-club-night-formats-compared", "/guides/game-variety-stop-same-players")]
    [InlineData("/features/club-nights", "/guides/game-variety-stop-same-players")]
    // badminton-club-rating-systems
    [InlineData("/guides/game-variety-stop-same-players", "/guides/badminton-club-rating-systems")]
    [InlineData("/guides/mixed-ability-badminton-club-night", "/guides/badminton-club-rating-systems")]
    [InlineData("/guides/how-to-run-handicap-doubles-tournament", "/guides/badminton-club-rating-systems")]
    [InlineData("/for-players", "/guides/badminton-club-rating-systems")]
    // picking-league-teams-club-data
    [InlineData("/guides/badminton-club-rating-systems", "/guides/picking-league-teams-club-data")]
    [InlineData("/features/league-team-support", "/guides/picking-league-teams-club-data")]
    // how-to-start-a-badminton-club
    [InlineData("/guides/how-to-run-a-badminton-club-night", "/guides/how-to-start-a-badminton-club")]
    public async Task Related_pages_link_to_the_revised_guides_from_their_content(string source, string target)
    {
        var content = MainContent(await Client().GetStringAsync(source));

        Assert.Contains($"href=\"{target}\"", content);
    }

    /// <summary>
    /// articles.md item 19: no FAQPage JSON-LD on a guide. The visible FAQ stays.
    /// dateModified must show the revision while datePublished keeps the first-publication date.
    /// </summary>
    [Theory]
    [MemberData(nameof(RevisedGuidesFirstPublished))]
    public async Task Revised_guides_emit_one_Article_with_the_revision_date_and_no_FAQPage(string path, string firstPublished)
    {
        var html = await Client().GetStringAsync(path);

        var blocks = Regex.Matches(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline)
            .Select(m => JsonDocument.Parse(WebUtility.HtmlDecode(m.Groups[1].Value)).RootElement)
            .ToList();
        var types = blocks.Select(b => b.GetProperty("@type").GetString()).ToList();

        Assert.DoesNotContain("FAQPage", types);
        var article = Assert.Single(blocks, b => b.GetProperty("@type").GetString() == "Article");
        Assert.Equal(Revised, article.GetProperty("dateModified").GetString());
        Assert.Equal(firstPublished, article.GetProperty("datePublished").GetString());
        Assert.Contains("<h2 id=\"faq\">", MainContent(html));
    }

    [Theory]
    [MemberData(nameof(RevisedGuides))]
    public async Task Sitemap_lastmod_matches_the_revision_date(string path)
    {
        var sitemap = await Client().GetStringAsync("/sitemap.xml");

        var m = Regex.Match(sitemap,
            $"<loc>https://www\\.epegboard\\.com{Regex.Escape(path)}</loc>\\s*<lastmod>([^<]+)</lastmod>");
        Assert.True(m.Success, $"{path} is missing from the sitemap");
        Assert.Equal(Revised, m.Groups[1].Value);
    }

    /// <summary>Every guide the sitemap lists, so a guide added later is covered without editing this.</summary>
    public static TheoryData<string> SitemapGuides()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "PegboardWebSite", "wwwroot", "sitemap.xml"));
        var data = new TheoryData<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(path), "<loc>https://www\\.epegboard\\.com(/guides/[^<]+)</loc>"))
            data.Add(m.Groups[1].Value);
        return data;
    }

    [Fact]
    public void The_sitemap_lists_guides()
    {
        // Guards the derived set above: an empty set would make every guide theory vacuous.
        Assert.True(SitemapGuides().Count >= 30);
    }

    [Theory]
    [MemberData(nameof(SitemapGuides))]
    public async Task Every_sitemap_guide_answers_200_and_is_listed_on_the_guides_index(string path)
    {
        var client = Client();

        var res = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var index = MainContent(await client.GetStringAsync("/guides"));
        Assert.Contains($"href=\"{path}\"", index);
    }

    /// <summary>
    /// The rating-systems guide shipped with a chat assistant's private-use citation markers
    /// ("citeturn999353search0...") in its rendered text. A reader sees them as garbage, and they
    /// mark the page as unedited machine output.
    /// </summary>
    [Theory]
    [MemberData(nameof(SitemapGuides))]
    public async Task No_guide_renders_leaked_assistant_citation_markers(string path)
    {
        var content = MainContent(await Client().GetStringAsync(path));

        Assert.DoesNotMatch(LeakedCitation, content);
    }

    [Fact]
    public void The_leaked_citation_pattern_fires_on_the_marker_that_shipped()
    {
        const string shipped = "uncertainty. \uE200cite\uE202turn999353search0\uE202turn999353search3\uE201</p>";

        Assert.Matches(LeakedCitation, shipped);
        Assert.DoesNotMatch(LeakedCitation, "Microsoft's TrueSkill similarly represents skill.</p>");
    }

    private static readonly Regex LeakedCitation = new(@"[\uE000-\uF8FF]|\bturn\d+search\d+\b|\bciteturn");
}
