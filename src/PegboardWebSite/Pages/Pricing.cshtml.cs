using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Net;
using PegboardWebSite.Services;

public class PricingModel : PageModel
{
    [BindProperty]
    public PurchaseRequestModel PurchaseRequest { get; set; } = new();

    public bool RequestSent { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            string body = $@"New purchase request:

Name: {PurchaseRequest.Name}
Club: {PurchaseRequest.ClubName}
Email: {PurchaseRequest.Email}
";
            EmailService emailService = new EmailService();
            emailService.SendMailToEPegboard("New Purchase Request", body);
            RequestSent = true;
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Could not send the request: " + ex.Message);
        }

        return Page();
    }
}

public class PurchaseRequestModel
{
    [Required]
    public string Name { get; set; }

    [Required]
    [Display(Name = "Club Name")]
    public string ClubName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }
}
