using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Services;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class RequestFeatureModel : PageModel
{
    private readonly EmailService _emailService;
    public RequestFeatureModel(EmailService emailService)
    {
        _emailService = emailService;
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

        try
        {
            _emailService.SendMailToEPegboard("Feature Request from Website", $"From: {FeatureRequest.Name} ({FeatureRequest.Email})\n\n{FeatureRequest.Description}");

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

        [Required]
        public string Description { get; set; }
    }
}
