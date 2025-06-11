using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Services;

namespace PegboardWebSite.Pages;
public class IndexModel : PageModel
{
    TrackedRequestRepository _requestRepository;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger, TrackedRequestRepository requestRepository)
    {
        _logger = logger;
        _requestRepository = requestRepository;
    }

    public void OnGet(string id)
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
        _requestRepository.Insert(new TrackedRequestModel()
        {
            RequestedResource = "Home Page",
            TrackingId = trackingId,
            Timestamp = DateTime.Now,
            SourceIP = RequestHelper.GetClientIp(HttpContext)
        });
    }
}
