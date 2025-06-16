using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using PegboardWebSite.Services;
using PegboardWebSite;

public class DownloadLinkModel : PageModel
{
    private readonly ILogger<DownloadLinkModel> _logger;

    private readonly TrackedRequestRepository _requestRepository;
    public DownloadLinkModel(TrackedRequestRepository requestRepo, ILogger<DownloadLinkModel> logger)
    {
        _logger = logger;
        _requestRepository = requestRepo;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    public string VersionToUse { get; set; }

    public void OnGet()
    {
        VersionToUse = Version ?? GetLatestVersion();
        LogDownloadIntent(Id, VersionToUse);


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

    private void LogDownloadIntent(string id, string version)
    {
        var log = $"{DateTime.UtcNow:u} | Intent to download | ID: {id} | Version: {version}";
        _logger.LogInformation(log);
    }

    private string GetLatestVersion() => "1.6.0"; // Placeholder
}
