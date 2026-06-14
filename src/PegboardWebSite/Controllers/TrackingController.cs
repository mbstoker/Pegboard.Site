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
    // Campaign open pixel. The send engine emits {websiteUrl}/track/o/{trackerId}; trackerId maps to
    // the outbound EmailMessage (MessageId = <trackerId@epegboard.com>) so the internal tracking-sync
    // can advance the recipient. Resource discriminator "open". Best-effort insert; always serves the
    // pixel so a tracking-DB blip never shows a broken image.
    [HttpGet("o/{trackerId}")]
    public IActionResult Open(string trackerId)
    {
        Record("open", trackerId);
        var path = Path.Combine(_env.WebRootPath, "images", "pixel.png");
        if (!System.IO.File.Exists(path)) return File(new byte[0], "image/png");
        return File(System.IO.File.ReadAllBytes(path), "image/png");
    }

    // Campaign click redirect. Records "click" then 302s to the play app. Fixed destination (no
    // arbitrary ?u=) to avoid an open-redirect; override via Tracking:ClickDestination config.
    [HttpGet("c/{trackerId}")]
    public IActionResult Click(string trackerId, [FromServices] IConfiguration config)
    {
        Record("click", trackerId);
        var dest = config["Tracking:ClickDestination"] ?? "https://play.epegboard.com";
        return Redirect(dest);
    }

    // One-click unsubscribe. Records "unsubscribe" with the club token; the internal tracking-sync
    // turns that into a suppression + club opt-out. Also accepts a POST for RFC 8058 one-click.
    [HttpGet("u/{token}")]
    [HttpPost("u/{token}")]
    public IActionResult Unsubscribe(string token)
    {
        Record("unsubscribe", token);
        const string html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Unsubscribed</title>" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>" +
            "<body style=\"font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:560px;margin:60px auto;padding:0 20px;color:#1f2d3d\">" +
            "<h2>You're unsubscribed</h2><p>You will not receive any more ePegboard emails. " +
            "If this was a mistake, just reply to one of our emails and we'll add you back.</p></body></html>";
        return Content(html, "text/html");
    }

    private void Record(string resource, string? trackingId)
    {
        _requestRepo.Insert(new TrackedRequestModel
        {
            RequestedResource = resource,
            TrackingId = trackingId,
            Timestamp = DateTime.Now,
            SourceIP = RequestHelper.GetClientIp(HttpContext),
        });
    }

    // Read endpoint the internal Marketing.Api tracking-sync polls (Mike's chosen pull model). Returns
    // tracked hits at/after minTime as JSON. Same shape as the existing consumer; unchanged contract.
    [HttpGet("requests")]
    public JsonResult Requests(DateTime? minTime = null)
    {
        List<TrackedRequestModel> requests = _requestRepo.GetAll(minTime);

        return Json(requests);
    }
}