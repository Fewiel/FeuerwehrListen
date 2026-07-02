using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;

namespace FeuerwehrListen.Services;

public class ListNotificationService
{
    private readonly SettingsService _settingsService;
    private readonly PdfExportService _pdfExportService;
    private readonly EmailSenderService _emailSenderService;
    private readonly AttendanceEntryRepository _attendanceEntryRepository;
    private readonly OperationEntryRepository _operationEntryRepository;
    private readonly FireSafetyWatchEntryRepository _fireSafetyWatchEntryRepository;
    private readonly OperationReportRepository _operationReportRepository;
    private readonly ILogger<ListNotificationService> _logger;

    public ListNotificationService(
        SettingsService settingsService,
        PdfExportService pdfExportService,
        EmailSenderService emailSenderService,
        AttendanceEntryRepository attendanceEntryRepository,
        OperationEntryRepository operationEntryRepository,
        FireSafetyWatchEntryRepository fireSafetyWatchEntryRepository,
        OperationReportRepository operationReportRepository,
        ILogger<ListNotificationService> logger)
    {
        _settingsService = settingsService;
        _pdfExportService = pdfExportService;
        _emailSenderService = emailSenderService;
        _attendanceEntryRepository = attendanceEntryRepository;
        _operationEntryRepository = operationEntryRepository;
        _fireSafetyWatchEntryRepository = fireSafetyWatchEntryRepository;
        _operationReportRepository = operationReportRepository;
        _logger = logger;
    }

    /// <summary>
    /// Prüft, ob leere Listen vom Mailversand ausgeschlossen sind.
    /// Standard (Setting nicht gesetzt) = true.
    /// </summary>
    private bool SkipEmptyLists()
    {
        var value = _settingsService.GetSetting(SettingKeys.NotificationSkipEmptyLists);
        return string.IsNullOrWhiteSpace(value)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase);
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

            if (SkipEmptyLists() && await _attendanceEntryRepository.CountByListIdAsync(list.Id) == 0)
            {
                _logger.LogInformation("Attendance list {ListId} is empty. Notification skipped (SkipEmptyLists).", list.Id);
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
            if (SkipEmptyLists() && await _operationEntryRepository.CountByListIdAsync(list.Id) == 0)
            {
                _logger.LogInformation("Operation list {ListId} is empty. Notification skipped (SkipEmptyLists).", list.Id);
                return;
            }

            var settings = await _settingsService.GetAllSettingsAsync();
            var recipientsRaw = settings.TryGetValue(SettingKeys.NotificationOperationRecipients, out var value) ? value : string.Empty;
            if (string.IsNullOrWhiteSpace(recipientsRaw))
            {
                _logger.LogInformation("No operation recipients configured.");
                return;
            }

            var pdf = await _pdfExportService.ExportOperationListAsync(list.Id);
            var subject = $"Abgeschlossene Einsatzliste: {list.OperationNumber}";
            var fileName = $"Einsatzliste_{list.Id}_{DateTime.Now:yyyyMMdd}.pdf";

            var attachments = new List<EmailAttachment> { new(pdf, fileName) };

            // Einsatzbericht mitschicken, sofern einer angelegt/ausgefüllt wurde
            var report = await _operationReportRepository.GetByOperationListIdAsync(list.Id);
            var hasReport = report != null;
            if (hasReport)
            {
                var reportPdf = await _pdfExportService.ExportOperationReportAsync(list.Id);
                attachments.Add(new EmailAttachment(reportPdf, $"Einsatzbericht_{list.Id}_{DateTime.Now:yyyyMMdd}.pdf"));
            }

            var body = hasReport
                ? $"Die Einsatzliste '{list.OperationNumber}' wurde abgeschlossen. Beigefügt: Einsatzliste und Einsatzbericht als PDF."
                : $"Die Einsatzliste '{list.OperationNumber}' wurde abgeschlossen und ist als PDF beigefügt.";

            await _emailSenderService.SendWithAttachmentsAsync(new[] { recipientsRaw }, subject, body, attachments);
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
            if (SkipEmptyLists() && await _fireSafetyWatchEntryRepository.CountByWatchIdAsync(watch.Id) == 0)
            {
                _logger.LogInformation("Fire safety watch {WatchId} is empty. Notification skipped (SkipEmptyLists).", watch.Id);
                return;
            }

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
