using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// Guards the club-management page split (ado:1898).
///
/// /badminton-club-management-software is the product-neutral category page and holds the
/// site's largest share of search impressions, so it must keep resolving at its own URL and
/// never redirect. /features/club-management is ePegboard's own attendance, payments and
/// reports page, and its title must not drift back into a variant of the category title -
/// that near-duplicate is what split the signal between them. The category page only earns
/// authority if the admin guides and the comparison pages link to it, and the guide owns
/// "badminton club software", so the category page must link there rather than compete.
/// </summary>
public sealed class ClubManagementCategoryTests : IClassFixture<TrackApiFactory>
{
    private const string Category = "/badminton-club-management-software";

    private readonly TrackApiFactory _factory;

    public ClubManagementCategoryTests(TrackApiFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string Title(string html) =>
        WebUtility.HtmlDecode(Regex.Match(html, "<title>([^<]*)</title>").Groups[1].Value);

    private static string H1(string html) =>
        Regex.Replace(Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value, "<[^>]+>", "");

    [Fact]
    public async Task Category_url_resolves_without_redirect()
    {
        var response = await Client().GetAsync(Category);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Category_page_carries_the_category_term_in_title_and_h1()
    {
        var html = await Client().GetStringAsync(Category);

        Assert.Contains("badminton club management software", Title(html), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("badminton club management software", H1(html), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Feature_page_title_is_not_a_variant_of_the_category_title()
    {
        var title = Title(await Client().GetStringAsync("/features/club-management"));

        Assert.DoesNotContain("club management", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("club software", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Category_page_names_what_epegboard_does_not_do()
    {
        var html = await Client().GetStringAsync(Category);

        Assert.Contains("Membership administration", html);
        Assert.Contains("Collecting subs", html);
        Assert.Contains("Court booking", html);
    }

    [Theory]
    [InlineData("/guides/best-badminton-club-night-software")]
    [InlineData("/compare")]
    [InlineData("/compare/method")]
    [InlineData("/features/club-management")]
    public async Task Category_page_links_to_its_neighbours(string target)
    {
        var html = await Client().GetStringAsync(Category);

        Assert.Contains($"href=\"{target}\"", html);
    }

    [Theory]
    [InlineData("/compare")]
    [InlineData("/guides/best-badminton-club-night-software")]
    [InlineData("/features/club-management")]
    [InlineData("/guides/collecting-club-subs")]
    [InlineData("/guides/setting-club-subs")]
    [InlineData("/guides/badminton-court-hire")]
    [InlineData("/guides/badminton-club-committee-roles")]
    [InlineData("/guides/badminton-club-constitution")]
    [InlineData("/guides/badminton-club-insurance")]
    [InlineData("/guides/how-to-start-a-badminton-club")]
    public async Task Pages_touching_club_administration_link_to_the_category_page(string source)
    {
        var html = await Client().GetStringAsync(source);

        Assert.Contains($"href=\"{Category}\"", html);
    }

    /// <summary>
    /// The guide owns "badminton club software" on the 2026-09-12 query-filtered evidence;
    /// /compare is the page that does not own it, so it links to the guide.
    /// </summary>
    [Fact]
    public async Task Compare_links_to_the_guide_that_owns_badminton_club_software()
    {
        var html = await Client().GetStringAsync("/compare");

        Assert.Contains(
            "<a href=\"/guides/best-badminton-club-night-software\">how to choose badminton club software</a>",
            html);
    }

    /// <summary>
    /// Every named product on the category page links to its own site, matching the
    /// /compare/method sourcing rule. Planted negative: a product row with no outbound link
    /// would leave its name without an href and fail here.
    /// </summary>
    [Theory]
    [InlineData("ShuttleOps", "https://www.shuttleops.com/pricing")]
    [InlineData("ShuttleStats", "https://shuttlestats.app/learn")]
    [InlineData("Spond Club", "https://help.spond.com/club/en/articles/177554-overview-payments-in-spond-club")]
    [InlineData("SportyHQ", "https://www.sportyhq.com/")]
    [InlineData("ServeLeague", "https://serveleague.com/")]
    [InlineData("ClubSpark", "https://www.clubspark.com/")]
    public async Task Each_named_product_links_to_its_own_source(string product, string url)
    {
        var html = await Client().GetStringAsync(Category);

        Assert.Contains($"<a href=\"{url}\" target=\"_blank\" rel=\"noopener\">{product}</a>", html);
    }
}
