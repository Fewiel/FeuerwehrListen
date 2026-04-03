using FeuerwehrListen.Models;

namespace FeuerwehrListen.Services;

public class ListNotificationService
{
    private readonly SettingsService _settingsService;
    private readonly PdfExportService _pdfExportService;
    private readonly EmailSenderService _emailSenderService;
    private readonly ILogger<ListNotificationService> _logger;

    public ListNotificationService(
        SettingsService settingsService,
        PdfExportService pdfExportService,
        EmailSenderService emailSenderService,
        ILogger<ListNotificationService> logger)
    {
        _settingsService = settingsService;
        _pdfExportService = pdfExportService;
        _emailSenderService = emailSenderService;
        _logger = logger;
    }

    public async Task NotifyAttendanceClosedAsync(AttendanceList list)
    {
        try
        {
            if (!list.UnitNumber.HasValue)
            {
                _logger.LogInformation("Attendance list {ListId} has no unit number. Notification skipped.", list.Id);
                return;
            }

            var settings = await _settingsService.GetAllSettingsAsync();
            var recipientKey = SettingKeys.GetAttendanceRecipientsKey(list.UnitNumber.Value);
            var recipientsRaw = settings.TryGetValue(recipientKey, out var value) ? value : string.Empty;
            if (string.IsNullOrWhiteSpace(recipientsRaw))
            {
                _logger.LogInformation("No attendance recipients configured for unit {Unit}.", list.UnitNumber.Value);
                return;
            }

            var pdf = await _pdfExportService.ExportAttendanceListAsync(list.Id);
            var subject = $"Abgeschlossene Anwesenheitsliste Einheit {list.UnitNumber}: {list.Title}";
            var body = $"Die Anwesenheitsliste '{list.Title}' wurde abgeschlossen und ist als PDF beigefugt.";
            var fileName = $"Anwesenheitsliste_Einheit{list.UnitNumber}_{list.Id}_{DateTime.Now:yyyyMMdd}.pdf";

            await _emailSenderService.SendWithAttachmentAsync(new[] { recipientsRaw }, subject, body, pdf, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Attendance close notification failed for list {ListId}", list.Id);
        }
    }

    public async Task NotifyOperationClosedAsync(OperationList list)
    {
        try
        {
            var settings = await _settingsService.GetAllSettingsAsync();
            var recipientsRaw = settings.TryGetValue(SettingKeys.NotificationOperationRecipients, out var value) ? value : string.Empty;
            if (string.IsNullOrWhiteSpace(recipientsRaw))
            {
                _logger.LogInformation("No operation recipients configured.");
                return;
            }

            var pdf = await _pdfExportService.ExportOperationListAsync(list.Id);
            var subject = $"Abgeschlossene Einsatzliste: {list.OperationNumber}";
            var body = $"Die Einsatzliste '{list.OperationNumber}' wurde abgeschlossen und ist als PDF beigefugt.";
            var fileName = $"Einsatzliste_{list.Id}_{DateTime.Now:yyyyMMdd}.pdf";

            await _emailSenderService.SendWithAttachmentAsync(new[] { recipientsRaw }, subject, body, pdf, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation close notification failed for list {ListId}", list.Id);
        }
    }

    public async Task NotifyFireSafetyWatchClosedAsync(FireSafetyWatch watch)
    {
        try
        {
            var settings = await _settingsService.GetAllSettingsAsync();
            var recipientsRaw = settings.TryGetValue(SettingKeys.NotificationFireSafetyWatchRecipients, out var value) ? value : string.Empty;
            if (string.IsNullOrWhiteSpace(recipientsRaw))
            {
                _logger.LogInformation("No fire safety watch recipients configured.");
                return;
            }

            var pdf = await _pdfExportService.ExportFireSafetyWatchAsync(watch.Id);
            var subject = $"Abgeschlossene Brandsicherheitswache: {watch.Name}";
            var body = $"Die Brandsicherheitswache '{watch.Name}' wurde abgeschlossen und ist als PDF beigefugt.";
            var fileName = $"Brandsicherheitswache_{watch.Id}_{DateTime.Now:yyyyMMdd}.pdf";

            await _emailSenderService.SendWithAttachmentAsync(new[] { recipientsRaw }, subject, body, pdf, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fire safety watch close notification failed for watch {WatchId}", watch.Id);
        }
    }
}
