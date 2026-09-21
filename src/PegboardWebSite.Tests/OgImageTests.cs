using System.Text.RegularExpressions;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// Contract guard for the og:image / twitter:image tags emitted by BOTH layouts.
///
/// Why this is worth a test when the rest of the marketing copy is not: the OgImage
/// resolution lives in _Layout.cshtml and _LayoutMarketing.cshtml, so it runs in the
/// &lt;head&gt; of every page on the site, and its failure mode is silent. A relative
/// og:image is not rendered wrong and it does not 500 - scrapers simply drop it, and the
/// share degrades to no card at all rather than to the default one. Nothing on the page
/// or in the logs says so; you find out when someone pastes a link into a group chat.
///
/// Hermetic and in-process via TrackApiFactory. WebsiteUrl is pinned here rather than
/// taken from appsettings so the expected absolute URL is a constant, not whatever host
/// the Development config happens to name.
/// </summary>
public sealed class OgImageTests : IClassFixture<TrackApiFactory>
{
    private const string BaseUrl = "https://og-test.example";

    private readonly TrackApiFactory _factory;

    public OgImageTests(TrackApiFactory factory) => _factory = factory;

    private async Task<string> GetHtml(string path)
    {
        var client = _factory
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:WebsiteUrl", BaseUrl))
            .CreateClient();
        return await client.GetStringAsync(path);
    }

    private static string? MetaContent(string html, string attr, string name)
    {
        var m = Regex.Match(html, $"<meta\\s+{attr}=\"{Regex.Escape(name)}\"\\s+content=\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/features/club-nights")]
    [InlineData("/pricing")]
    public async Task Page_without_its_own_card_gets_the_absolute_default(string path)
    {
        var html = await GetHtml(path);

        Assert.Equal($"{BaseUrl}/Images/og-image.png", MetaContent(html, "property", "og:image"));
        Assert.Equal($"{BaseUrl}/Images/og-image.png", MetaContent(html, "name", "twitter:image"));
    }

    [Fact]
    public async Task Page_with_its_own_card_gets_that_card_made_absolute()
    {
        var html = await GetHtml("/badminton-club-management-software");

        var expected = $"{BaseUrl}/Images/og-badminton-club-management-software.png";
        Assert.Equal(expected, MetaContent(html, "property", "og:image"));
        Assert.Equal(expected, MetaContent(html, "name", "twitter:image"));
    }

    /// <summary>
    /// The page sets a site-RELATIVE path; the layout is what makes it absolute. This is the
    /// assertion that actually fails if that resolution is ever dropped, because a relative
    /// value still renders a perfectly valid-looking tag.
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/badminton-club-management-software")]
    public async Task Og_image_is_always_an_absolute_http_url(string path)
    {
        var value = MetaContent(await GetHtml(path), "property", "og:image");

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.True(
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
            $"og:image on {path} is not an absolute http(s) URL: '{value}'");
    }

    /// <summary>
    /// _LayoutMarketing.cshtml and _Layout.cshtml hard-code og:image:width/height as 1200x630,
    /// so every card any page names has to actually be that size. Before per-page cards there
    /// was one image and it always matched; now the meta is falsifiable, and LinkedIn in
    /// particular rejects a card whose declared size does not match the file.
    /// </summary>
    [Fact]
    public async Task The_declared_card_is_really_1200x630()
    {
        var html = await GetHtml("/badminton-club-management-software");
        var url = new Uri(MetaContent(html, "property", "og:image")!);

        Assert.Equal("1200", MetaContent(html, "property", "og:image:width"));
        Assert.Equal("630", MetaContent(html, "property", "og:image:height"));

        var client = _factory.CreateClient();
        var png = await client.GetByteArrayAsync(url.AbsolutePath);

        // PNG IHDR: 8-byte signature, 4-byte length, "IHDR", then width and height big-endian.
        Assert.True(png.Length > 24, "card did not download");
        var width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Assert.Equal(1200, width);
        Assert.Equal(630, height);
    }
}
