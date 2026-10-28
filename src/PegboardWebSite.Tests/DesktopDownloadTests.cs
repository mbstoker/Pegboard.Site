using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PegboardWebSite.Services;
using Xunit;

namespace PegboardWebSite.Tests;

/// <summary>
/// The Windows desktop download (ado:2173): the emailed link lands on /downloadlink, whose
/// button fetches /downloadfinal. Each test gets its own temporary downloads folder, so the
/// set of installers "on the server" is exactly what the test puts there.
///
/// Guards the four faults that left the download 404ing in production: the advertised
/// version must come from the files present (no placeholder), the form must post the
/// parameter the handler binds, a requested version that is not there must say so rather
/// than fall through to the latest, and an empty folder must say so rather than offer a
/// button that 404s.
/// </summary>
public sealed class DesktopDownloadTests : IClassFixture<TrackApiFactory>, IDisposable
{
    private readonly TrackApiFactory _factory;
    private readonly string _dir;

    public DesktopDownloadTests(TrackApiFactory factory)
    {
        _factory = factory;
        _dir = Path.Combine(Path.GetTempPath(), "pegsite-downloads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private void Publish(string version, int bytes = 2048)
    {
        var data = new byte[bytes];
        // OLE compound-file signature, which is what an MSI starts with.
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(data, 0);
        File.WriteAllBytes(Path.Combine(_dir, DesktopInstallerCatalog.FileNameFor(version)), data);
    }

    private HttpClient Client() =>
        _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                s.AddSingleton(new DesktopInstallerCatalog(_dir))))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // --- Catalog: the advertised version is derived from the files present.

    [Fact]
    public void Latest_is_the_highest_version_by_number_not_by_text()
    {
        Publish("1.3.9");
        Publish("1.3.34");
        Publish("1.10.0");
        File.WriteAllText(Path.Combine(_dir, "ePegboardSetup_notaversion.msi"), "x");
        File.WriteAllText(Path.Combine(_dir, "epegboard user guide.pdf"), "x");

        var catalog = new DesktopInstallerCatalog(_dir);

        Assert.Equal("1.10.0", catalog.Latest()?.Version);
        Assert.Equal(new[] { "1.10.0", "1.3.34", "1.3.9" }, catalog.Available().Select(i => i.Version));
    }

    [Fact]
    public void Missing_folder_means_nothing_available()
    {
        var catalog = new DesktopInstallerCatalog(Path.Combine(_dir, "does-not-exist"));
        Assert.Equal(InstallerLookupStatus.NoneAvailable, catalog.Find(null).Status);
    }

    [Theory]
    [InlineData("../appsettings")]
    [InlineData("..\\..\\web")]
    [InlineData("1.3.34/../../x")]
    [InlineData("latest")]
    [InlineData("1")]
    public void Malformed_version_is_refused_before_touching_the_file_system(string requested)
    {
        Publish("1.3.34");
        Assert.Equal(InstallerLookupStatus.InvalidVersion, new DesktopInstallerCatalog(_dir).Find(requested).Status);
    }

    // --- /downloadlink: what the emailed link shows.

    [Fact]
    public async Task Download_link_advertises_the_published_version_and_posts_it_as_v()
    {
        Publish("1.3.34");
        var html = await Client().GetStringAsync("/downloadlink?id=abc");

        Assert.Contains("version 1.3.34", html);
        Assert.Matches(new Regex("<input type=\"hidden\" name=\"v\" value=\"1.3.34\""), html);
        Assert.DoesNotContain("name=\"version\"", html);
        Assert.DoesNotContain("1.6.0", html);
    }

    [Fact]
    public async Task Download_link_with_no_installer_says_so_instead_of_offering_a_dead_button()
    {
        var html = await Client().GetStringAsync("/downloadlink?id=abc");

        Assert.Contains("data-testid=\"download-unavailable\"", html);
        Assert.Contains("not available", html);
        Assert.DoesNotContain("action=\"/downloadfinal\"", html);
    }

    [Fact]
    public async Task Download_link_for_a_version_that_does_not_exist_says_so()
    {
        Publish("1.3.34");
        var html = await Client().GetStringAsync("/downloadlink?id=abc&v=1.6.0");

        Assert.Contains("Version 1.6.0 of the ePegboard desktop app is not available", html);
        Assert.Contains("The current version is 1.3.34", html);
        Assert.DoesNotContain("action=\"/downloadfinal\"", html);
    }

    // --- /downloadfinal: the file itself.

    [Fact]
    public async Task Final_with_no_version_serves_the_latest_installer_as_an_msi()
    {
        Publish("1.3.30");
        Publish("1.3.34", bytes: 4096);

        var res = await Client().GetAsync("/downloadfinal?id=abc");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(DesktopInstallerCatalog.MsiContentType, res.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ePegboardSetup_1.3.34.msi", res.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(4096, (await res.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Final_serves_exactly_the_requested_version()
    {
        Publish("1.3.30", bytes: 3000);
        Publish("1.3.34", bytes: 4096);

        var res = await Client().GetAsync("/downloadfinal?id=abc&v=1.3.30");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("ePegboardSetup_1.3.30.msi", res.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(3000, (await res.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Final_for_a_missing_version_is_404_naming_it_and_does_not_fall_through_to_latest()
    {
        Publish("1.3.34");

        var res = await Client().GetAsync("/downloadfinal?id=abc&v=1.6.0");
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Contains("Version 1.6.0", body);
        Assert.Contains("1.3.34", body);
        Assert.Null(res.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task Final_with_a_malformed_version_is_400_and_does_not_echo_it()
    {
        Publish("1.3.34");

        var res = await Client().GetAsync("/downloadfinal?v=..%2F..%2Fappsettings");
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.DoesNotContain("appsettings", body);
    }

    [Fact]
    public async Task Final_with_nothing_published_is_404_saying_so()
    {
        var res = await Client().GetAsync("/downloadfinal?id=abc");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Contains("not available", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_version_the_link_page_posts_is_one_the_final_endpoint_serves()
    {
        Publish("1.3.30");
        Publish("1.3.34");
        var client = Client();

        var html = await client.GetStringAsync("/downloadlink?id=abc");
        var posted = Regex.Match(html, "name=\"v\" value=\"([^\"]+)\"").Groups[1].Value;
        var res = await client.GetAsync($"/downloadfinal?id=abc&v={posted}");

        Assert.Equal("1.3.34", posted);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal($"ePegboardSetup_{posted}.msi", res.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Head_reports_the_installer_without_sending_it()
    {
        Publish("1.3.34", bytes: 4096);

        var res = await Client().SendAsync(new HttpRequestMessage(HttpMethod.Head, "/downloadfinal"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(DesktopInstallerCatalog.MsiContentType, res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(4096, res.Content.Headers.ContentLength);
        Assert.Empty(await res.Content.ReadAsByteArrayAsync());
    }

    // --- Download tracking: the daily download check uses HEAD, so HEAD must not count.

    [Fact]
    public async Task Head_records_no_download_and_get_records_one()
    {
        Publish("1.3.34");
        var recorder = new RecordingTrackedRequests();
        var client = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.AddSingleton(new DesktopInstallerCatalog(_dir));
                s.AddSingleton<TrackedRequestRepository>(recorder);
            }))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/downloadfinal?id=check&v=1.3.34"));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Empty(recorder.Inserted);

        var get = await client.GetAsync("/downloadfinal?id=club-42&v=1.3.34");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var row = Assert.Single(recorder.Inserted);
        Assert.Equal("ePegboardSetup_1.3.34.msi", row.RequestedResource);
        Assert.Equal("club-42", row.TrackingId);
    }

    /// <summary>Records inserts in memory instead of writing to PostgreSQL.</summary>
    private sealed class RecordingTrackedRequests : TrackedRequestRepository
    {
        private readonly List<TrackedRequestModel> _inserted = new();

        public RecordingTrackedRequests()
            : base(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                   Microsoft.Extensions.Logging.Abstractions.NullLogger<TrackedRequestRepository>.Instance) { }

        public IReadOnlyList<TrackedRequestModel> Inserted { get { lock (_inserted) return _inserted.ToList(); } }

        public override void Insert(TrackedRequestModel request) { lock (_inserted) _inserted.Add(request); }
    }
}
