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

    public async Task<bool> SendWithAttachmentAsync(
        IEnumerable<string> recipients,
        string subject,
        string body,
        byte[] attachmentBytes,
        string attachmentFileName)
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
                Body = body
            };

            foreach (var recipient in normalizedRecipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            var stream = new MemoryStream(attachmentBytes);
            message.Attachments.Add(new Attachment(stream, attachmentFileName, "application/pdf"));

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
