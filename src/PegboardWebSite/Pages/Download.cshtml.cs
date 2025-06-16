using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Net;
using PegboardWebSite.Services;
using PegboardWebSite.Pages;
using Org.BouncyCastle.Security;
using PegboardWebSite;

public class DownloadModel : PageModel
{
    private readonly ILogger<DownloadModel> _logger;
    private readonly EmailService _emailService;
    private readonly TrackedRequestRepository _requestRepository;

    public DownloadModel(TrackedRequestRepository requestRepo, EmailService emailService, ILogger<DownloadModel> logger)
    {
        _emailService = emailService;
        _logger = logger;
        _requestRepository = requestRepo;
    }

    [BindProperty]
    public UserDownloadInfo UserInfo { get; set; } = new();

    public bool EmailSent { get; set; } = false;

    public void OnGet(string? name, string? club, string? email)
    {
        if (!string.IsNullOrEmpty(name)) UserInfo.Name = name;
        if (!string.IsNullOrEmpty(club)) UserInfo.ClubName = club;
        if (!string.IsNullOrEmpty(email)) UserInfo.Email = email;

    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            if (IsSpam(UserInfo))
            {
                _logger.LogWarning($"Spam download blocked Name: {UserInfo.Name} Club: {UserInfo.ClubName} Email: {UserInfo.Email}");
                _emailService.SendMailToEPegboard("Spam download blocked", $"Name: {UserInfo.Name} Club: {UserInfo.ClubName} Email: {UserInfo.Email}");
            }
            else
            {
                _emailService.SendDownloadLink(UserInfo.Name, UserInfo.ClubName, UserInfo.Email);
                _emailService.SendMailToEPegboard("Download link sent", $"Name: {UserInfo.Name} Club: {UserInfo.ClubName} Email: {UserInfo.Email}");

                var trackingId = HttpContext.Session.GetString("TrackingId");
                if (!string.IsNullOrEmpty(trackingId))
                {
                    _requestRepository.Insert(new TrackedRequestModel()
                    {
                        RequestedResource = "DownloadLinkEmail",
                        TrackingId = trackingId,
                        Timestamp = DateTime.Now,
                        SourceIP = RequestHelper.GetClientIp(HttpContext)
                    });
                }

            }

            EmailSent = true;

        }
        catch (Exception ex)
        {
            _logger.LogError("An error occurred while sending email: " + ex.Message);
            ModelState.AddModelError("", "An error occurred while sending email: " + ex.Message);
        }

        return Page();
    }


    private bool IsSpam(UserDownloadInfo userInfo)
    {
        return IsSpamName(userInfo.Name) || IsSpamName(userInfo.ClubName);
    }

    private bool IsSpamName(string name)
    {
        if (name.Length > 50)
            return true;

        if (name.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '\'' && c != '-' && c != ',' && c != '(' && c != ')'))
        {
            return true;
        }
        return false;
    }
}

public class UserDownloadInfo
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
