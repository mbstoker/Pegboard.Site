using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// The previous site served its FAQ at /FAQs. Google still holds that URL and sends searchers to
/// it, so it must permanently redirect to /faq, the page that replaced it, in any casing.
///
/// The http and apex variants are canonicalised to https://www.epegboard.com/{path} by the IIS
/// rules in web.config, which keep the path, so they arrive here as /FAQs and take this redirect.
/// /faq itself must still answer 200, which also proves the redirect cannot loop.
/// </summary>
public sealed class LegacyFaqRedirectTests : IClassFixture<TrackApiFactory>
{
    private readonly TrackApiFactory _factory;

    public LegacyFaqRedirectTests(TrackApiFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/FAQs")]
    [InlineData("/faqs")]
    [InlineData("/FAQS")]
    [InlineData("/FAQs/")]
    [InlineData("/FAQs?utm_source=google")]
    public async Task Legacy_faqs_url_permanently_redirects_to_faq(string path)
    {
        var res = await NoRedirectClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.MovedPermanently, res.StatusCode);
        Assert.Equal("/faq", res.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Faq_page_answers_200_without_redirecting()
    {
        var res = await NoRedirectClient().GetAsync("/faq");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Following_the_legacy_url_lands_on_the_faq_page()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var res = await client.GetAsync("/FAQs");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("/faq", res.RequestMessage?.RequestUri?.AbsolutePath);
    }
}
