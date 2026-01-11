using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;
using System;

namespace TripsProject.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            var sendGrid = _config.GetSection("SendGrid");

            var apiKey = sendGrid["ApiKey"];
            var fromEmail = sendGrid["FromEmail"];
            var fromName = sendGrid["FromName"];

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("SendGrid ApiKey is missing");

            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new Exception("SendGrid FromEmail is missing");

            if (string.IsNullOrWhiteSpace(fromName))
                throw new Exception("SendGrid FromName is missing");

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var toAddress = new EmailAddress(to);

            var msg = MailHelper.CreateSingleEmail(
                from,
                toAddress,
                subject,
                plainTextContent: body,
                htmlContent: body
            );

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode != 202)
            {
                var bodyContent = await response.Body.ReadAsStringAsync();
                throw new Exception($"SendGrid failed: {response.StatusCode} - {bodyContent}");
            }
        }
    }
}
