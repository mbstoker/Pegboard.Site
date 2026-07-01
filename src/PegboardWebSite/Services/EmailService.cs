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
        string body = $"Hi {name},\n\nThanks for your interest in ePegboard!\n\nClick here to download: {_websiteUrl}/downloadlink?id={trackingId}\n\nBest,\nMike";
        SendMail("mike.stoker@epegboard.com", email, "ePegboard Download Link", body);

        _logger.LogInformation($"Download link sent.  Name: {name}  Club: {clubName} Email: {email}  TrackingID: {trackingId}");
    }

    public void SendMail(string from, string to, string subject, string body)
    {
        // SMTP settings come from configuration (Email:Smtp). The password is supplied
        // per-environment on the VPS via the Email__Smtp__Password environment variable and
        // is deliberately not held in source control. Non-secret defaults are kept as a fallback.
        var smtpConfig = _configuration.GetSection("Email:Smtp");
        var host = smtpConfig["Host"] ?? "smtp.ionos.co.uk";
        var port = int.TryParse(smtpConfig["Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !bool.TryParse(smtpConfig["EnableSsl"], out var parsedSsl) || parsedSsl;
        // On the VPS the credentials come from the existing EPEGBOARD_SMTP_* machine
        // environment variables; fall back to Email:Smtp config (used locally / for overrides).
        var username = Environment.GetEnvironmentVariable("EPEGBOARD_SMTP_USERNAME");
        if (string.IsNullOrEmpty(username)) username = smtpConfig["Username"];

        var password = Environment.GetEnvironmentVariable("EPEGBOARD_SMTP_PASSWORD");
        if (string.IsNullOrEmpty(password)) password = smtpConfig["Password"];

        if (string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("SMTP password is not configured - set the EPEGBOARD_SMTP_PASSWORD environment variable on the host. Outbound email will fail to authenticate.");
        }

        var message = new MailMessage(from, to)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        using (var smtp = new SmtpClient(host, port))
        {
            smtp.Credentials = new NetworkCredential(username, password);
            smtp.EnableSsl = enableSsl;
            smtp.Send(message);
        }

        _logger.LogInformation($"Email sent. From: {from}  To: {to}  Subject: {subject}");
    }
}
