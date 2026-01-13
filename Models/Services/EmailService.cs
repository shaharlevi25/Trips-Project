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
                var apiKey =
                _config["SendGrid:ApiKey"]
                ?? Environment.GetEnvironmentVariable("SENDGRID__ApiKey");
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
        
        
        public async Task SendWithAttachmentAsync(
            string to,
            string subject,
            string htmlBody,
            byte[]? attachmentBytes,
            string? attachmentFileName,
            string? attachmentMimeType
        )
        {
            var apiKey = _config["SendGrid:ApiKey"]
                         ?? Environment.GetEnvironmentVariable("SENDGRID__ApiKey");

            var fromEmail = _config["SendGrid:FromEmail"];
            var fromName = _config["SendGrid:FromName"];

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("SendGrid ApiKey is missing");
            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new Exception("SendGrid FromEmail is missing");
            if (string.IsNullOrWhiteSpace(fromName))
                throw new Exception("SendGrid FromName is missing");

            var client = new SendGridClient(apiKey);

            var msg = MailHelper.CreateSingleEmail(
                new EmailAddress(fromEmail, fromName),
                new EmailAddress(to),
                subject,
                plainTextContent: StripHtml(htmlBody),
                htmlContent: htmlBody
            );

            if (attachmentBytes != null && attachmentBytes.Length > 0)
            {
                msg.AddAttachment(
                    attachmentFileName ?? "invoice.pdf",
                    Convert.ToBase64String(attachmentBytes),
                    attachmentMimeType ?? "application/pdf",
                    disposition: "attachment"
                );
            }

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode != 202)
            {
                var bodyContent = await response.Body.ReadAsStringAsync();
                throw new Exception($"SendGrid failed: {response.StatusCode} - {bodyContent}");
            }
        }
        private static string StripHtml(string html)
        {
            // מינימלי כדי שיהיה PlainText סביר
            return html
                .Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n")
                .Replace("</p>", "\n").Replace("<p>", "")
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("<strong>", "").Replace("</strong>", "")
                .Replace("<b>", "").Replace("</b>", "")
                .Replace("<h2>", "").Replace("</h2>", "\n")
                .Replace("<h3>", "").Replace("</h3>", "\n");
        }
    }
}
