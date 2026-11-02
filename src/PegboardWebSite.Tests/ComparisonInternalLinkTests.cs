using System.Text.RegularExpressions;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// Guards the editorial routes into the comparison pages (ado:2395).
///
/// /compare and /compare/method were listed in the sitemap but sat in "Discovered - currently
/// not indexed": Google knew of them and had not fetched them, which on a small site means few
/// internal links point at them. The pages Google already indexes and ranks must each link to
/// both from their own content. The layout's nav and footer do not count, so the assertion is
/// scoped to &lt;main&gt;.
/// </summary>
public sealed class ComparisonInternalLinkTests : IClassFixture<TrackApiFactory>
{
    private readonly TrackApiFactory _factory;

    public ComparisonInternalLinkTests(TrackApiFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>The page's own content, so a nav or footer link cannot satisfy the assertion.</summary>
    private static string MainContent(string html)
    {
        var m = Regex.Match(html, "<main>(.*)</main>", RegexOptions.Singleline);
        Assert.True(m.Success, "No <main> element in the rendered page");
        return m.Groups[1].Value;
    }

    [Theory]
    [InlineData("/", "/compare")]
    [InlineData("/", "/compare/method")]
    [InlineData("/badminton-club-management-software", "/compare")]
    [InlineData("/badminton-club-management-software", "/compare/method")]
    [InlineData("/guides/best-badminton-club-night-software", "/compare")]
    [InlineData("/guides/best-badminton-club-night-software", "/compare/method")]
    public async Task Indexed_pages_link_to_the_comparison_pages_from_their_content(string source, string target)
    {
        var content = MainContent(await Client().GetStringAsync(source));

        Assert.Contains($"href=\"{target}\"", content);
    }
}
