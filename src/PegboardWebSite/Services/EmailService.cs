using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;

namespace PegboardWebSite.Services;

public class EmailService
{
    public void SendMailToEPegboard(string subject, string body)
    {
        SendMail("noreply@epegboard.com", "mike.stoker@epegboard.com", subject, body);
    }

    public void SendDownloadLink(string name, string clubName, string email)
    {
        string body = $"Hi {name},\n\nThanks for your interest!\n\nClick here to download: https://epegboard.com/downloadlink?id=1234\n\nBest,\nMike";
        SendMail("mike.stoker@epegboard.com", email, "Your Download Link", body);
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
    }
}
