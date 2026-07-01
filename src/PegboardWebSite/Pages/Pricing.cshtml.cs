using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Net;
using PegboardWebSite.Services;
using PegboardWebSite;

public class PricingModel : PageModel
{
    private readonly EmailService _emailService;
    private readonly ILogger _logger;

    public PricingModel(EmailService emailService, ILogger<PricingModel> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    [BindProperty]
    public PurchaseRequestModel PurchaseRequest { get; set; } = new();

    public bool RequestSent { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        string ip = RequestHelper.GetClientIp(HttpContext);
        if (Spam.IsSpamName(PurchaseRequest.Name) || Spam.IsSpamName(PurchaseRequest.ClubName))
        {
            _logger.LogWarning($"Spam purchase request blocked Name: {PurchaseRequest.Name} Club: {PurchaseRequest.ClubName} Email: {PurchaseRequest.Email} Ip: {ip}");
            RequestSent = true;
            return Page();
        }

        try
        {
            string body = $@"New purchase request:

Name: {PurchaseRequest.Name}
Club: {PurchaseRequest.ClubName}
Email: {PurchaseRequest.Email}
";
            _emailService.SendMailToEPegboard("New Purchase Request", body);
            RequestSent = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send purchase request email from the website.");
            ModelState.AddModelError("", "Sorry, we could not send your request. Please email sales@epegboard.com.");
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
