using Microsoft.AspNetCore.Mvc;

namespace PegboardWebSite.Controllers;

[Route("track")]
public class TrackingController : Controller
{
    private readonly ILogger<TrackingController> _logger;
    private readonly IWebHostEnvironment _env;

    public TrackingController(ILogger<TrackingController> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    [HttpGet("email-open")]
    public IActionResult EmailOpen(string campaignId, string recipientId)
    {
        // 🔍 Log the email open
        _logger.LogInformation("Email opened: Campaign = {CampaignId}, Recipient = {RecipientId}, IP = {IP}",
            campaignId, recipientId, HttpContext.Connection.RemoteIpAddress?.ToString());

        // 📷 Serve the tracking pixel (a tiny transparent PNG)
        var path = Path.Combine(_env.WebRootPath, "images", "pixel.png"); // e.g. wwwroot/images/pixel.png
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var imageData = System.IO.File.ReadAllBytes(path);
        return File(imageData, "image/png");
    }
}