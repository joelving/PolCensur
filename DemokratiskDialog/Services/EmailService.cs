using DemokratiskDialog.Options;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class EmailService
    {
        public EmailService(IOptionsSnapshot<EmailServiceOptions> optionsAccessor)
        {
            Options = optionsAccessor.Value;
        }

        public EmailServiceOptions Options { get; } //set only via Secret Manager

        public Task SendEmailAsync(string email, string subject, string message)
        {
            var client = new SendGridClient(Options.SendGridKey);
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(Options.SenderAddress, Options.SenderName),
                Subject = subject,
                PlainTextContent = message,
                HtmlContent = message
            };
            msg.AddTo(new EmailAddress(email));
            return client.SendEmailAsync(msg);
        }
    }
}
