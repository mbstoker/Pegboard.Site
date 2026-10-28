using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PegboardWebSite.Services;
using PegboardWebSite;

/// <summary>
/// Serves the Windows installer. <c>v</c> is the version parameter, and it is the name the
/// DownloadLink form posts (ado:2173: the form used to post <c>version</c>, which this handler
/// never bound, so every request silently fell through to "latest").
///
/// No <c>v</c> = the latest installer present. A supplied <c>v</c> is served exactly or
/// refused with a 404 that names the version; it never falls back to the latest.
/// HEAD requests are answered (so the download check can verify the file without pulling
/// it) but are not recorded as downloads.
/// </summary>
public class DownloadFinalModel : PageModel
{
    private readonly TrackedRequestRepository _requestRepository;
    private readonly DesktopInstallerCatalog _catalog;
    private readonly ILogger _logger;

    public DownloadFinalModel(DesktopInstallerCatalog catalog, TrackedRequestRepository requestRepository, ILogger<DownloadFinalModel> logger)
    {
        _catalog = catalog;
        _requestRepository = requestRepository;
        _logger = logger;
    }

    public IActionResult OnGet(string? id, string? v)
    {
        string? trackingId = id;
        if (!string.IsNullOrEmpty(trackingId))
        {
            HttpContext.Session.SetString("TrackingId", trackingId);
        }
        else
        {
            trackingId = HttpContext.Session.GetString("TrackingId");
        }

        var lookup = _catalog.Find(v);
        if (lookup.Status != InstallerLookupStatus.Found || lookup.Installer == null || !System.IO.File.Exists(lookup.Installer.FullPath))
        {
            _logger.LogWarning("Desktop download refused: {Status} (requested {Requested}, latest {Latest})",
                lookup.Status, lookup.Requested ?? (string.IsNullOrEmpty(v) ? "latest" : "invalid"), lookup.Latest?.Version ?? "none");

            var status = lookup.Status == InstallerLookupStatus.InvalidVersion
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status404NotFound;
            var message = lookup.Status == InstallerLookupStatus.Found
                ? new InstallerLookup(InstallerLookupStatus.NoneAvailable, null, null, null).Explanation
                : lookup.Explanation;
            return new ContentResult { StatusCode = status, Content = message, ContentType = "text/plain; charset=utf-8" };
        }

        var installer = lookup.Installer;

        if (HttpMethods.IsGet(Request.Method))
        {
            _requestRepository.Insert(new TrackedRequestModel() { RequestedResource = installer.FileName, TrackingId = trackingId, Timestamp = DateTime.Now, SourceIP = RequestHelper.GetClientIp(HttpContext) });
            _logger.LogInformation("{Time:u} | File served | ID: {TrackingId} | Version: {Version}", DateTime.UtcNow, trackingId, installer.Version);
        }

        return PhysicalFile(installer.FullPath, DesktopInstallerCatalog.MsiContentType, installer.FileName);
    }
}
