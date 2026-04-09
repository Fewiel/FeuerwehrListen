namespace FeuerwehrListen.Models;

public static class SettingKeys
{
    public const string VisibilityAttendance = "ModuleVisibility.Attendance";
    public const string VisibilityOperations = "ModuleVisibility.Operations";
    public const string VisibilityFireSafetyWatch = "ModuleVisibility.FireSafetyWatch";

    public const string AutoCloseAttendance = "AutoClose.AttendanceMinutes";
    public const string AutoCloseOperations = "AutoClose.OperationMinutes";
    public const string AutoCloseFireSafetyWatch = "AutoClose.FireSafetyWatchMinutes";

    public const string NotificationAttendanceRecipientsPrefix = "Notifications.AttendanceRecipients.Unit.";
    public const string NotificationOperationRecipients = "Notifications.OperationRecipients";
    public const string NotificationFireSafetyWatchRecipients = "Notifications.FireSafetyWatchRecipients";

    public const string VisibilityDefects = "ModuleVisibility.Defects";
    public const string NotificationDefectRecipients = "Notifications.DefectRecipients";

    public const string SmtpHost = "Smtp.Host";
    public const string SmtpPort = "Smtp.Port";
    public const string SmtpUsername = "Smtp.Username";
    public const string SmtpPassword = "Smtp.Password";
    public const string SmtpFromAddress = "Smtp.FromAddress";
    public const string SmtpUseSsl = "Smtp.UseSsl";

    public const string SoundEnabled = "Sound.Enabled";
    public const string BrandingLogoUrl = "Branding.LogoUrl";
    public const string BrandingAppName = "Branding.AppName";
    public const string QrReorderRecipients = "Notifications.QrReorderRecipients";

    public static string GetAttendanceRecipientsKey(int unitNumber) =>
        $"{NotificationAttendanceRecipientsPrefix}{unitNumber}";
}
