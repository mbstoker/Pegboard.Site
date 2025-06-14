using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using PegboardWebSite.Services;

namespace PegboardWebSite.Controllers;

[Route("track")]
public class TrackingController : Controller
{
    private readonly ILogger<TrackingController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly TrackedRequestRepository _requestRepo;

    public TrackingController(TrackedRequestRepository requestRepo, ILogger<TrackingController> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
        _requestRepo = requestRepo;
    }

    [HttpGet("email-open")]
    public IActionResult EmailOpen(string campaignId, string recipientId)
    {
        string sourceIp = RequestHelper.GetClientIp(HttpContext);
        // 🔍 Log the email open
        _logger.LogInformation("Email opened: Campaign = {CampaignId}, Recipient = {RecipientId}, IP = {IP}",
            campaignId, recipientId, sourceIp);

        _requestRepo.Insert(new TrackedRequestModel
        {
            RequestedResource = "Email Open",
            TrackingId = recipientId,
            Timestamp = DateTime.Now,
            SourceIP = sourceIp
        });

        // 📷 Serve the tracking pixel (a tiny transparent PNG)
        var path = Path.Combine(_env.WebRootPath, "images", "pixel.png"); // e.g. wwwroot/images/pixel.png
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var imageData = System.IO.File.ReadAllBytes(path);
        return File(imageData, "image/png");

    }
    // GET: Sessions
    [HttpGet("requests")]
    public JsonResult Requests(DateTime? minTime = null)
    {
        List<TrackedRequestModel> requests = _requestRepo.GetAll(minTime);

        return Json(requests);
    }
}