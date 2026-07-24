using FlowDesk.Options;
using FlowDesk.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace FlowDesk.Services
{
    public sealed class BrevoEmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public BrevoEmailService(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(
            string recipientEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    _settings.SenderName,
                    _settings.SenderEmail));

            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = subject;

            message.Body = new TextPart(TextFormat.Html)
            {
                Text = htmlBody
            };

            using var smtpClient = new SmtpClient();

            await smtpClient.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTls,
                cancellationToken);

            await smtpClient.AuthenticateAsync(
                _settings.Username,
                _settings.Password,
                cancellationToken);

            await smtpClient.SendAsync(
                message,
                cancellationToken);

            await smtpClient.DisconnectAsync(
                true,
                cancellationToken);
        }
    }
}