using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FlyNotify.Services
{
    /*
        Static notification service responsible for authenticating and 
        sending transactional flight alerts via the Gmail SMTP servers.
    */
    public static class EmailService
    {
        private const string SmtpServer = "smtp.gmail.com";
        private const int SmtpPort = 587;
        private const string SenderEmail = "coastalmacs@gmail.com";
        private const string SenderPassword = "yaptazmyflxbdqpi";
        private const string RecipientEmail = "coastalmacs@gmail.com";

        /*
            Sends an email alert to the recipient containing the batch query changes.
        */
        public static async Task SendStatusAlertAsync(string changeSummary)
        {
            try
            {
                using var message = new MailMessage();
                message.From = new MailAddress(SenderEmail, "FlyNotify");
                message.To.Add(new MailAddress(RecipientEmail));
                message.Subject = "FlyNotify - Flight Status Changes Alert";
                message.Body = $"The following flight status changes were detected during the latest query check:\n\n{changeSummary}";

                using var smtpClient = new SmtpClient(SmtpServer, SmtpPort);
                smtpClient.Credentials = new NetworkCredential(SenderEmail, SenderPassword);
                smtpClient.EnableSsl = true;

                await smtpClient.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Email Sending Exception]: {ex.Message}");
            }
        }
    }
}
