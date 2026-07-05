using System.Net;
using System.Net.Mail;
using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

public class EmailSenderService
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<EmailSenderService> _logger;

    public EmailSenderService(SettingsService settingsService, ILogger<EmailSenderService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public Task<bool> SendAsync(IEnumerable<string> recipients, string subject, string body)
        => SendWithAttachmentsAsync(recipients, subject, body, Array.Empty<EmailAttachment>());

    public Task<bool> SendWithAttachmentAsync(
        IEnumerable<string> recipients,
        string subject,
        string body,
        byte[]? attachmentBytes,
        string? attachmentFileName)
    {
        var attachments = attachmentBytes != null && !string.IsNullOrEmpty(attachmentFileName)
            ? new[] { new EmailAttachment(attachmentBytes, attachmentFileName!) }
            : Array.Empty<EmailAttachment>();
        return SendWithAttachmentsAsync(recipients, subject, body, attachments);
    }

    public async Task<bool> SendWithAttachmentsAsync(
        IEnumerable<string> recipients,
        string subject,
        string body,
        IEnumerable<EmailAttachment> attachments)
    {
        var settings = await _settingsService.GetAllSettingsAsync();

        var host = GetString(settings, SettingKeys.SmtpHost);
        var from = GetString(settings, SettingKeys.SmtpFromAddress);
        var username = GetString(settings, SettingKeys.SmtpUsername);
        var password = GetString(settings, SettingKeys.SmtpPassword);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogWarning("Email skipped: SMTP host or from-address is not configured.");
            return false;
        }

        var port = GetInt(settings, SettingKeys.SmtpPort, 587);
        var useSsl = GetBool(settings, SettingKeys.SmtpUseSsl, true);

        var normalizedRecipients = recipients
            .SelectMany(SplitRecipients)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedRecipients.Count == 0)
        {
            _logger.LogInformation("Email skipped: no recipients provided.");
            return false;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from),
                Subject = subject,
                Body = body,
                SubjectEncoding = System.Text.Encoding.UTF8,
                BodyEncoding = System.Text.Encoding.UTF8
            };

            foreach (var recipient in normalizedRecipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            foreach (var att in attachments)
            {
                if (att.Bytes == null || att.Bytes.Length == 0 || string.IsNullOrEmpty(att.FileName))
                    continue;
                var stream = new MemoryStream(att.Bytes);
                message.Attachments.Add(new Attachment(stream, att.FileName, "application/pdf"));
            }

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email with subject '{Subject}'.", subject);
            return false;
        }
    }

    private static IEnumerable<string> SplitRecipients(string raw)
    {
        return raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string GetString(Dictionary<string, string> settings, string key)
    {
        return settings.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static int GetInt(Dictionary<string, string> settings, string key, int fallback)
    {
        return settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool GetBool(Dictionary<string, string> settings, string key, bool fallback)
    {
        return settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }
}

public record EmailAttachment(byte[] Bytes, string FileName);
