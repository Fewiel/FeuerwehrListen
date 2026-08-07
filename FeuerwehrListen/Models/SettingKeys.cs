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

    public const string VisibilityCalendar = "ModuleVisibility.Calendar";

    /// <summary>
    /// Oeffentliche Basis-URL (scheme://host), z. B. https://listen.feuerwehr-x.de.
    /// Noetig fuer absolute Links in Mails (Freigabe-Links) - hinter einem Reverse-Proxy
    /// ist der Host-Header sonst der interne. Hat Vorrang vor AppSettings:BaseUrl.
    /// </summary>
    public const string AppBaseUrl = "App.BaseUrl";

    /// <summary>Gueltigkeitsdauer eines Freigabe-Links in Stunden (Standard 168 = 7 Tage).</summary>
    public const string CalendarApprovalTokenHours = "Calendar.ApprovalTokenHours";

    // --- Zugriffsschutz ---

    /// <summary>Hauptschalter: ausserhalb der vertrauenswuerdigen Netze nur noch die
    /// freigegebenen Module fuer nicht angemeldete Besucher.</summary>
    public const string SecurityRestrictExternal = "Security.RestrictExternal";

    /// <summary>Als "lokal" geltende Netze in CIDR-Schreibweise, kommagetrennt.</summary>
    public const string SecurityTrustedNetworks = "Security.TrustedNetworks";

    /// <summary>IPs/Netze des eigenen Reverse-Proxys. NUR von diesen wird
    /// X-Forwarded-For ausgewertet - sonst waere die Netz-Schranke faelschbar.</summary>
    public const string SecurityTrustedProxies = "Security.TrustedProxies";

    /// <summary>Module, die auch von extern ohne Login nutzbar bleiben.</summary>
    public const string SecurityExternalModules = "Security.ExternalModules";

    /// <summary>Host-Profile, ein Eintrag je Zeile: "host = modul1, modul2".</summary>
    public const string SecurityHostProfiles = "Security.HostProfiles";

    /// <summary>Host-Profile auch fuer angemeldete Benutzer erzwingen (Standard: nein,
    /// damit man sich nicht selbst aussperrt).</summary>
    public const string SecurityHostProfilesApplyToLoggedIn = "Security.HostProfilesApplyToLoggedIn";

    /// <summary>Empfänger für Einsatz-Feedback (eine oder mehrere Adressen).</summary>
    public const string NotificationFeedbackRecipients = "Notifications.FeedbackRecipients";

    /// <summary>
    /// Wenn "true": Abgeschlossene Listen ohne Einträge werden NICHT per Mail versendet.
    /// Standard (nicht gesetzt) = true.
    /// </summary>
    public const string NotificationSkipEmptyLists = "Notifications.SkipEmptyLists";

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

    // Nextcloud (WebDAV) für Einsatzbilder
    public const string NextcloudUrl = "Nextcloud.Url";
    public const string NextcloudUsername = "Nextcloud.Username";
    public const string NextcloudAppPassword = "Nextcloud.AppPassword";
    public const string NextcloudBasePath = "Nextcloud.BasePath";

    // Optionaler Alias/Name je Einheit 1-9 (z. B. "3" -> "Jugendfeuerwehr")
    public const string UnitAliasPrefix = "Unit.Alias.";

    public static string GetAttendanceRecipientsKey(int unitNumber) =>
        $"{NotificationAttendanceRecipientsPrefix}{unitNumber}";

    public static string GetUnitAliasKey(int unitNumber) =>
        $"{UnitAliasPrefix}{unitNumber}";
}
