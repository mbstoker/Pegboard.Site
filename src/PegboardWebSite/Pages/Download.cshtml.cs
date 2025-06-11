using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Net;
using PegboardWebSite.Services;

public class DownloadModel : PageModel
{
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
            var emailService = new EmailService();
            emailService.SendDownloadLink(UserInfo.Name, UserInfo.ClubName, UserInfo.Email);

            EmailSent = true;
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occurred while sending email: " + ex.Message);
        }

        return Page();
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
