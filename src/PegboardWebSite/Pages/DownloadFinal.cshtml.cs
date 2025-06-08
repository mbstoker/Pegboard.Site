using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PegboardWebSite.Services;
using PegboardWebSite; // Needed for Session extensions

public class DownloadFinalModel : PageModel
{
    TrackedRequestRepository _requestRepository;
    private readonly IWebHostEnvironment _env;

    public DownloadFinalModel(IWebHostEnvironment env, TrackedRequestRepository requestRepository)
    {
        _env = env;
        _requestRepository = requestRepository;
    }

    public async Task<IActionResult> OnGetAsync(string id, string? v)
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

        var version = string.IsNullOrEmpty(v) ? "latest" : v;
        var filePath = GetFilePath(version);

        if (filePath == null)
        {
            return NotFound("Requested version not found.");
        }

        string fileName = Path.GetFileName(filePath);

        _requestRepository.Insert(new TrackedRequestModel() { RequestedResource = fileName, TrackingId = trackingId, Timestamp = DateTime.Now, SourceIP = RequestHelper.GetClientIp(HttpContext) });

        if (!System.IO.File.Exists(filePath))
            return NotFound("Requested version not found.");

        var contentType = "application/octet-stream";
        return PhysicalFile(filePath, contentType, fileName);
    }

    private void LogFinalDownload(string id, string version)
    {
        var log = $"{DateTime.UtcNow:u} | File served | ID: {id} | Version: {version}";
        System.IO.File.AppendAllText("App_Data/download_log.txt", log + Environment.NewLine);
    }

    private string GetFilePath(string version)
    {
        var dir = Path.Combine(_env.WebRootPath, "downloads");

        string filePath;
        if (version == "latest")
        {
            var files = Directory.GetFiles(dir, "ePegboardSetup_*.msi");
            Version highest = null;
            string latestFile = null;

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file); // ePegboardSetup_1.3.0
                var versionPart = name.Substring("ePegboardSetup_".Length);

                if (Version.TryParse(versionPart, out Version parsedVersion))
                {
                    if (highest == null || parsedVersion > highest)
                    {
                        highest = parsedVersion;
                        latestFile = Path.GetFileName(file);
                    }
                }
            }

            if (latestFile == null)
                return null;

            return Path.Combine(dir, latestFile);
        }
        else
        {
            return Path.Combine(dir, $"ePegboardSetup_{version}.msi");
        }
    }
}
