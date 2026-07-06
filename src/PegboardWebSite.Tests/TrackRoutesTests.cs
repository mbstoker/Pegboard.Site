using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// Contract / route-existence guards for the marketing Site's email-attribution + unsubscribe
/// endpoints (backlog #657, ADR-0010 seam-guard). HQ's email tracking-sync and RFC 8058
/// unsubscribe compliance depend on these exact route shapes; a breaking change must fail HERE
/// (in CI), not in prod on the venture's proven acquisition channel.
///
/// All tests are in-process (WebApplicationFactory) and hermetic - see TrackApiFactory.
/// </summary>
public sealed class TrackRoutesTests : IClassFixture<TrackApiFactory>
{
    private readonly TrackApiFactory _factory;

    public TrackRoutesTests(TrackApiFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // --- Negative control: proves 404s actually happen, so "non-404" assertions below are meaningful.

    [Fact]
    public async Task Unknown_track_route_is_404()
    {
        var res = await NoRedirectClient().GetAsync("/track/definitely-not-a-real-route");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // --- /track/o/{trackerId} : campaign OPEN pixel. Records "open"; always serves an image.

    [Fact]
    public async Task Open_pixel_exists_and_returns_png()
    {
        var res = await NoRedirectClient().GetAsync("/track/o/tracker-abc123");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
    }

    // --- /track/c/{trackerId} : campaign CLICK. Records "click", then 302 -> play app (fixed dest).

    [Fact]
    public async Task Click_redirects_302_to_play_app()
    {
        var res = await NoRedirectClient().GetAsync("/track/c/tracker-abc123");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode); // 302 (not permanent)
        // Uri normalizes a host-only target with a trailing slash; semantically the play app root.
        Assert.Equal("https://play.epegboard.com/", res.Headers.Location?.ToString());
    }

    // --- /track/u/{token} : one-click UNSUBSCRIBE. RFC 8058 accepts BOTH GET and POST.

    [Fact]
    public async Task Unsubscribe_get_returns_200_html_confirmation()
    {
        var res = await NoRedirectClient().GetAsync("/track/u/club-token-xyz");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/html", res.Content.Headers.ContentType?.MediaType);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("unsubscribed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unsubscribe_post_one_click_returns_200()
    {
        // RFC 8058: List-Unsubscribe-Post=One-Click issues a POST to the same URL.
        var res = await NoRedirectClient().PostAsync("/track/u/club-token-xyz", new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("unsubscribed", body, StringComparison.OrdinalIgnoreCase);
    }

    // --- /track/requests?minTime= : JSON sync feed the Marketing.Api tracking-sync polls.
    // DB is offline in-test (by design), so GetAll swallows + returns [] -> the CONTRACT
    // (200 + JSON array shape) is what we guard, independent of DB contents.

    [Fact]
    public async Task Requests_feed_with_minTime_returns_200_json_array()
    {
        var res = await NoRedirectClient().GetAsync("/track/requests?minTime=2026-01-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
        var arr = await res.Content.ReadFromJsonAsync<List<TrackedRequestDto>>();
        Assert.NotNull(arr); // valid JSON array (empty when DB unreachable)
    }

    [Fact]
    public async Task Requests_feed_without_minTime_still_returns_200_json()
    {
        // minTime is optional (nullable) - the feed must still respond.
        var res = await NoRedirectClient().GetAsync("/track/requests");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
    }

    // --- /track/email-open : LEGACY open pixel (campaignId/recipientId query). Still routed; pixel exists.

    [Fact]
    public async Task Legacy_email_open_pixel_exists_and_returns_png()
    {
        var res = await NoRedirectClient()
            .GetAsync("/track/email-open?campaignId=camp-1&recipientId=rcpt-1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
    }

    // --- ESCAPE-HATCH routes: /media/{id}.png and /r/{id} are routed OUTSIDE the /track prefix on
    // purpose (a "/track/..." URL screams tracking to spam filters). #657 asks us to confirm from
    // CODE whether these still EXIST after the deliverability rework removed them from email
    // TEMPLATES. FINDING: both routes are STILL PRESENT in TrackingController (Screenshot + Website
    // actions) - only the templates stopped emitting them. We therefore assert them as live.

    [Fact]
    public async Task Media_screenshot_beacon_still_exists_and_returns_image()
    {
        var res = await NoRedirectClient().GetAsync("/media/tracker-abc123.png");
        Assert.NotEqual(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.StartsWith("image/", res.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task R_website_click_still_exists_and_redirects_302_to_marketing_site()
    {
        var res = await NoRedirectClient().GetAsync("/r/tracker-abc123");
        Assert.NotEqual(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("https://www.epegboard.com/", res.Headers.Location?.ToString());
    }

    // --- ATTRIBUTION. The FF-S3a expectation was "?t={slug} on the demo link"; the CODE does NOT
    // implement a ?t= param anywhere. The REAL attribution mechanism is the homepage:
    // IndexModel.OnGet(string id) binds ?id={slug}, stashes it in session as "TrackingId", and
    // records a "Home Page" tracked request. We assert the mechanism that actually exists.

    [Fact]
    public async Task Homepage_accepts_attribution_id_param()
    {
        var res = await NoRedirectClient().GetAsync("/?id=outreach-slug-42");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("text/html", res.Content.Headers.ContentType?.ToString() ?? "");
    }

    [Fact]
    public async Task Homepage_works_without_attribution_param()
    {
        var res = await NoRedirectClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // --- /demo : legacy demo path from older outreach copy -> permanent (301) redirect to the
    // instant-demo flow on the play app. Part of the "demo link" surface #657 calls out.

    [Fact]
    public async Task Demo_path_permanent_redirects_to_instant_demo()
    {
        var res = await NoRedirectClient().GetAsync("/demo");
        Assert.Equal(HttpStatusCode.MovedPermanently, res.StatusCode); // 301
        Assert.Equal("https://play.epegboard.com/instant-demo", res.Headers.Location?.ToString());
    }

    // Minimal DTO mirroring the feed's JSON shape (TrackedRequestModel) for deserialization.
    private sealed record TrackedRequestDto(
        int Id, string? RequestedResource, string? TrackingId, DateTime Timestamp, string? SourceIP);
}
