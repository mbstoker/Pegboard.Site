using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

public class DownloadLinkModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    public string VersionToUse { get; set; } = "1.6.0";

    public void OnGet()
    {
        VersionToUse = Version ?? GetLatestVersion();
        LogDownloadIntent(Id, VersionToUse);
    }

    private void LogDownloadIntent(string id, string version)
    {
        var log = $"{DateTime.UtcNow:u} | Intent to download | ID: {id} | Version: {version}";
      //  System.IO.File.AppendAllText("App_Data/download_log.txt", log + Environment.NewLine);
    }

    private string GetLatestVersion() => "1.6.0"; // Placeholder
}
