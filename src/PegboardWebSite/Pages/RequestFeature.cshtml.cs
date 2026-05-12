using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Services;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class RequestFeatureModel : PageModel
{
    private readonly EmailService _emailService;
    private readonly ILogger<RequestFeatureModel> _logger;

    public RequestFeatureModel(EmailService emailService, ILogger<RequestFeatureModel> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    [BindProperty]
    public FeatureRequestModel FeatureRequest { get; set; }

    public bool EmailSent { get; set; } = false;

    public void OnGet()
    {
        FeatureRequest = new FeatureRequestModel();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Spam.IsSpamName(FeatureRequest.Name))
        {
            _logger.LogWarning($"Spam feature request blocked Name: {FeatureRequest.Name} Email: {FeatureRequest.Email}");
            EmailSent = true;
            return Page();
        }

        try
        {
            var versionLine = string.IsNullOrWhiteSpace(FeatureRequest.Version) ? "(not specified)" : FeatureRequest.Version;
            _emailService.SendMailToEPegboard("Feature Request from Website", $"From: {FeatureRequest.Name} ({FeatureRequest.Email})\nVersion: {versionLine}\n\n{FeatureRequest.Description}");

            EmailSent = true;
            ModelState.Clear();
        }
        catch
        {
            ModelState.AddModelError("", "Sorry, something went wrong while sending your report.");
        }

        return Page();
    }

    public class FeatureRequestModel
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
