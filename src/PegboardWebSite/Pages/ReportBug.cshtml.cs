using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Services;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class ReportBugModel : PageModel
{
    private readonly EmailService _emailService;
    private readonly ILogger<ReportBugModel> _logger;

    public ReportBugModel(EmailService emailService, ILogger<ReportBugModel> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    [BindProperty]
    public BugReportModel BugReport { get; set; }

    public bool EmailSent { get; set; } = false;

    public void OnGet()
    {
        BugReport = new BugReportModel();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Spam.IsSpamName(BugReport.Name))
        {
            _logger.LogWarning($"Spam bug report blocked Name: {BugReport.Name} Email: {BugReport.Email}");
            EmailSent = true;
            return Page();
        }

        try
        {
            var versionLine = string.IsNullOrWhiteSpace(BugReport.Version) ? "(not specified)" : BugReport.Version;
            _emailService.SendMailToEPegboard("Bug Report from Website", $"From: {BugReport.Name} ({BugReport.Email})\nVersion: {versionLine}\n\n{BugReport.Description}");

            EmailSent = true;
            ModelState.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bug report email from the website.");
            ModelState.AddModelError("", "Sorry, something went wrong while sending your report.");
        }

        return Page();
    }

    public class BugReportModel
    {
        [Required]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        public string Version { get; set; } = "";

        [Required]
        public string Description { get; set; }
    }
}
