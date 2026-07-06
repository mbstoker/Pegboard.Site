using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Services;

namespace PegboardWebSite.Pages;
public class IndexModel : PageModel
{
    TrackedRequestRepository _requestRepository;
    private readonly HomeStatsRepository _statsRepository;
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Homepage social-proof counts, read from the weekly-baked store. Initialised to the
    /// seed defaults so the view always has sane values even if the store read fails.
    /// </summary>
    public HomeStats Stats { get; private set; } = HomeStatsDefaults.Seed();

    public IndexModel(ILogger<IndexModel> logger, TrackedRequestRepository requestRepository, HomeStatsRepository statsRepository)
    {
        _logger = logger;
        _requestRepository = requestRepository;
        _statsRepository = statsRepository;
    }

    public void OnGet(string id)
    {
        // Render from the baked store; fall back to seed defaults if it is unreachable
        // (never let the social-proof figures flicker to zero/blank).
        Stats = _statsRepository.Read() ?? HomeStatsDefaults.Seed();

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
