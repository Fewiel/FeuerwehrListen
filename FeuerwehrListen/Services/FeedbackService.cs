using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

public class FeedbackService
{
    private readonly EmailSenderService _email;
    private readonly SettingsService _settings;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(EmailSenderService email, SettingsService settings, ILogger<FeedbackService> logger)
    {
        _email = email;
        _settings = settings;
        _logger = logger;
    }

    public enum FeedbackResult
    {
        Sent,
        NoRecipients,
        SendFailed
    }

    /// <summary>
    /// Sendet ein Feedback zu einem Einsatz an die in den Settings hinterlegten Empfänger.
    /// </summary>
    public async Task<FeedbackResult> SendFeedbackAsync(OperationList operation, string feedback, string? submitterName = null, string? submitterNumber = null)
    {
        var recipients = _settings.GetSetting(SettingKeys.NotificationFeedbackRecipients);
        if (string.IsNullOrWhiteSpace(recipients))
        {
            _logger.LogWarning("Feedback skipped: no feedback recipients configured.");
            return FeedbackResult.NoRecipients;
        }

        var operationNumber = NextcloudService.StripLeadingYear(operation.OperationNumber, operation.AlertTime.Year);
        // Absender (nicht anonym): Name + optional Mitgliedsnummer.
        var submitter = string.IsNullOrWhiteSpace(submitterName)
            ? (submitterNumber ?? "").Trim()
            : (string.IsNullOrWhiteSpace(submitterNumber) ? submitterName!.Trim() : $"{submitterName!.Trim()} ({submitterNumber!.Trim()})");
        var subject = $"Neues Feedback zu Einsatz {operationNumber}"
            + (string.IsNullOrWhiteSpace(submitter) ? "" : $" von {submitter}");

        // Normalisiere Zeilenumbrüche aus dem Textarea (kann \r\n, \r oder \n sein)
        var cleanFeedback = (feedback ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();

        var body =
            "Neues Feedback zu:\n\n" +
            $"Von: {(string.IsNullOrWhiteSpace(submitter) ? "—" : submitter)}\n" +
            $"Einsatz: {operationNumber}\n" +
            $"Stichwort: {operation.Keyword}\n" +
            $"Adresse: {operation.Address}\n" +
            $"Datum/Uhrzeit Alarmierung: {operation.AlertTime:dd.MM.yyyy HH:mm} Uhr\n\n" +
            "Feedback:\n" +
            cleanFeedback + "\n";

        var ok = await _email.SendAsync(new[] { recipients }, subject, body);
        return ok ? FeedbackResult.Sent : FeedbackResult.SendFailed;
    }
}
