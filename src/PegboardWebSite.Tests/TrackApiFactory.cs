using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PegboardWebSite;

namespace PegboardWebSite.Tests;

/// <summary>
/// In-process host for the marketing Site (WebApplicationFactory&lt;Program&gt;).
///
/// HERMETIC BY DESIGN: the tracking endpoints persist via TrackedRequestRepository,
/// whose Insert/GetAll swallow every DB exception (best-effort tracking - a dead DB
/// must never 5xx the request; see the 2026-05-31 outage note in that class). We force
/// ConnectionStrings:PegboardDb to empty here so NpgsqlConnection.Open() fails fast and
/// locally with NO network call - guaranteeing the suite runs offline in CI even if the
/// dev/CI machine happens to have a ConnectionStrings__PegboardDb environment variable set.
///
/// Content root is pinned to the web project so wwwroot assets (the tracking pixel served
/// by /track/o and /track/email-open) resolve during the test run.
/// </summary>
public sealed class TrackApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var webProjectDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PegboardWebSite"));
        if (Directory.Exists(webProjectDir))
        {
            builder.UseContentRoot(webProjectDir);
        }

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Force offline: empty connection string -> Npgsql fails fast, no network.
                ["ConnectionStrings:PegboardDb"] = string.Empty,
            });
        });
    }
}
