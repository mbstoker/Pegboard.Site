using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Services;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

public class ReportBugModel : PageModel
{
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

        try
        {
            new EmailService().SendMailToEPegboard("Bug Report from Website", $"From: {BugReport.Name} ({BugReport.Email})\n\n{BugReport.Description}");

            EmailSent = true;
            ModelState.Clear();
        }
        catch
        {
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

        [Required]
        public string Description { get; set; }
    }
}
