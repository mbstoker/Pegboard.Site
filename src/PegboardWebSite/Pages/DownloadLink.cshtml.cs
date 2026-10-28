using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using PegboardWebSite.Services;
using PegboardWebSite;

/// <summary>
/// The page the emailed download link lands on. It advertises the version it will serve,
/// taken from the installers actually present (ado:2173 - it used to hardcode "1.6.0"),
/// and posts that version to /downloadfinal as <c>v</c>, the name that handler binds.
/// A requested version that is not available is said so here, not passed on to 404.
/// </summary>
public class DownloadLinkModel : PageModel
{
    private readonly ILogger<DownloadLinkModel> _logger;
    private readonly TrackedRequestRepository _requestRepository;
    private readonly DesktopInstallerCatalog _catalog;

    public DownloadLinkModel(DesktopInstallerCatalog catalog, TrackedRequestRepository requestRepo, ILogger<DownloadLinkModel> logger)
    {
        _catalog = catalog;
        _logger = logger;
        _requestRepository = requestRepo;
    }

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>Optional specific version, same parameter name as /downloadfinal.</summary>
    [BindProperty(Name = "v", SupportsGet = true)]
    public string? Version { get; set; }

    /// <summary>The version the Download button will fetch; null when nothing can be served.</summary>
    public string? VersionToUse { get; private set; }

    /// <summary>Why nothing can be served, when <see cref="VersionToUse"/> is null.</summary>
    public string? Unavailable { get; private set; }

    public void OnGet()
    {
        var lookup = _catalog.Find(Version);
        if (lookup.Status == InstallerLookupStatus.Found && lookup.Installer != null)
        {
            VersionToUse = lookup.Installer.Version;
        }
        else
        {
            Unavailable = lookup.Explanation;
            _logger.LogWarning("Desktop download link cannot offer an installer: {Status}", lookup.Status);
        }

        _logger.LogInformation("{Time:u} | Intent to download | ID: {Id} | Version: {Version}", DateTime.UtcNow, Id, VersionToUse ?? "none");

        var trackingId = HttpContext.Session.GetString("TrackingId");
        if (!string.IsNullOrEmpty(trackingId))
        {
            _requestRepository.Insert(new TrackedRequestModel()
            {
                RequestedResource = "DownloadLink",
                TrackingId = trackingId,
                Timestamp = DateTime.Now,
                SourceIP = RequestHelper.GetClientIp(HttpContext)
            });
        }
    }
}
