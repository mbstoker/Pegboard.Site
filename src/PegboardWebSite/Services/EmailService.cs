using System.Net;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace PegboardWebSite.Services;

public class EmailService
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly string _websiteUrl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _logger = logger;
        _configuration = configuration;
        _websiteUrl = _configuration.GetConnectionString("WebsiteUrl")!;
    }

    public void SendMailToEPegboard(string subject, string body)
    {
        SendMail("noreply@epegboard.com", "mike.stoker@epegboard.com", subject, body);
    }

    public void SendDownloadLink(string name, string clubName, string email)
    {
        Guid trackingId = Guid.NewGuid();
        string body = $"Hi {name},\n\nThanks for your interest!\n\nClick here to download: {_websiteUrl}/downloadlink?id={trackingId}\n\nBest,\nMike";
        SendMail("mike.stoker@epegboard.com", email, "Your Download Link", body);

        _logger.LogInformation($"Download link sent.  Name: {name}  Club: {clubName} Email: {email}  TrackingID: {trackingId}");
    }

    public void SendMail(string from, string to, string subject, string body)
    {
        var message = new MailMessage(from, to);
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = false;

        using (var smtp = new SmtpClient("smtp.ionos.co.uk", 587))
        {
            smtp.Credentials = new System.Net.NetworkCredential("mike.stoker@epegboard.com", "Cr1cc13th!");
            smtp.EnableSsl = true;
            smtp.Send(message);
        }

        _logger.LogInformation($"Email sent. From: {from}  To: {to}  Subject: {subject}  Body: {body}");
    }
}
