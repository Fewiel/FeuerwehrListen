using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using FeuerwehrListen.Services;
using QRCoder;

namespace FeuerwehrListen.Endpoints;

/// <summary>
/// Mitgliederbereich: Profil, Lichtbild, Dienstausweis, oeffentliche Ausweispruefung,
/// Passwort-Vergabe am Gerätehaus-Tablet und die Konto-Verwaltung fuer Admins.
/// Siehe PLAN-MITGLIEDERBEREICH-FUEHRUNGSKREIS.md, Teil 1.
/// </summary>
public static class MemberAreaEndpoints
{
    private const int MaxPhotoBytes = 1024 * 1024;

    public static void MapMemberArea(this WebApplication app)
    {
        // ------------------------------------------------------------ eigenes Profil
        var me = app.MapGroup("/client-api/me").RequireAuthorization().DisableAntiforgery();

        me.MapGet("", async (HttpContext http, MemberRepository members, UserAccessService ua, SettingsService settings, AppUrlService urls) =>
        {
            var m = await CurrentMemberAsync(http, members, ua);
            if (m == null) return Results.Json(new { isMember = false });
            var info = ua.Get(http);
            return Results.Json(new
            {
                isMember = true,
                id = m.Id,
                number = m.MemberNumber,
                firstName = m.FirstName,
                lastName = m.LastName,
                rank = m.Rank,
                birthDate = m.BirthDate,
                unit = m.UnitNumber is int u ? settings.GetUnitLabel(u) : null,
                // Aussteller des Ausweises (Einstellung Card.IssuedBy).
                issuedBy = settings.GetSetting(SettingKeys.CardIssuedBy),
                email = m.Email,
                hasPhoto = !string.IsNullOrEmpty(m.PhotoFile),
                photoVersion = m.PhotoUpdatedAt?.Ticks ?? 0,
                leadershipCircle = m.LeadershipCircle,
                leadershipLabel = LeadershipCircles.Label(m.LeadershipCircle),
                modules = info?.Modules?.ToArray() ?? Array.Empty<string>(),
                card = CardDto(m, urls)
            });
        });

        me.MapGet("/photo", async (HttpContext http, MemberRepository members, UserAccessService ua, DataPathService paths) =>
        {
            var m = await CurrentMemberAsync(http, members, ua);
            return m == null ? Results.NotFound() : PhotoResult(m, paths);
        });

        // Lichtbild hochladen: der Browser schneidet zu und verkleinert (JPEG 4:5), hier nur pruefen.
        me.MapPost("/photo", async (HttpContext http, MemberRepository members, UserAccessService ua, DataPathService paths, PhotoUpload body) =>
        {
            var m = await CurrentMemberAsync(http, members, ua);
            if (m == null) return Results.Forbid();
            var error = await SavePhotoAsync(m, body.DataUrl, members, paths);
            return error == null ? Results.Json(new { ok = true, photoVersion = m.PhotoUpdatedAt?.Ticks ?? 0 }) : Results.Json(new { ok = false, error });
        });

        me.MapPut("/profile", async (HttpContext http, MemberRepository members, UserAccessService ua, ProfileUpdate body) =>
        {
            var m = await CurrentMemberAsync(http, members, ua);
            if (m == null) return Results.Forbid();
            var email = (body.Email ?? "").Trim();
            if (email.Length > 200 || (email.Length > 0 && !email.Contains('@'))) return Results.Json(new { ok = false, error = "email" });
            m.Email = email.Length == 0 ? null : email;
            await members.UpdateAsync(m);
            return Results.Json(new { ok = true });
        });

        // QR-Code des Dienstausweises als SVG (Inhalt: oeffentlicher Pruef-Link).
        me.MapGet("/card-qr", async (HttpContext http, MemberRepository members, UserAccessService ua, AppUrlService urls) =>
        {
            var m = await CurrentMemberAsync(http, members, ua);
            if (m == null || string.IsNullOrEmpty(m.CardPublicId)) return Results.NotFound();
            using var data = new QRCodeGenerator().CreateQrCode(CheckUrl(m, urls), QRCodeGenerator.ECCLevel.M);
            var svg = new SvgQRCode(data).GetGraphic(8, "#111111", "#FFFFFF", drawQuietZones: true);
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Text(svg, "image/svg+xml");
        });

        // Startseite des Mitgliederbereichs: naechste Termine, offene Wachen, eigene FK-Aufgaben.
        me.MapGet("/overview", async (HttpContext http, MemberRepository members, UserAccessService ua, NetworkAccessService access,
            CalendarRepository calendar, FireSafetyWatchRepository watches, LeadershipRepository leadership) =>
        {
            var m = await CurrentMemberAsync(http, members, ua);
            var info = ua.Get(http);
            if (info == null) return Results.Unauthorized();
            var allowed = access.GetAllowedModules(http);

            var events = new List<object>();
            if (allowed.Contains(NetworkAccessService.ModuleCalendar) || allowed.Contains(NetworkAccessService.ModuleLeadership))
            {
                // Einheiten des Mitglieds: Termine mit Einheitenzuordnung nur, wenn sie zur eigenen
                // Einheit gehoeren; ohne Zuordnung sehen alle. Fahrzeug-/Raumbuchungen nur eigene.
                var myUnits = m == null ? new HashSet<int>() : (await members.GetUnitsForMemberAsync(m.Id)).ToHashSet();
                if (m?.UnitNumber is int pu) myUnits.Add(pu);
                var from = DateTime.Now;
                var list = (await calendar.GetEventsInRangeAsync(from, from.AddDays(21)))
                    .Where(e => e.Status != CalendarEventStatus.Abgelehnt && e.Status != CalendarEventStatus.Storniert)
                    .Where(e => e.Type == CalendarEventType.Fuehrungskreis
                        ? info.CanSeeCircle(e.LeadershipCircle ?? LeadershipCircles.Small)
                        : allowed.Contains(NetworkAccessService.ModuleCalendar))
                    .Where(e => e.Type != CalendarEventType.Fahrzeugbuchung && e.Type != CalendarEventType.Raumbuchung
                        || (m != null && e.MemberId == m.Id))
                    .Where(e => e.UnitNumber is not int u || myUnits.Contains(u))
                    .OrderBy(e => e.StartTime).Take(5);
                events.AddRange(list.Select(e => new
                {
                    id = e.Id, title = e.Title, start = e.StartTime, location = e.Location,
                    leadership = e.Type == CalendarEventType.Fuehrungskreis
                }));
            }

            var openWatches = new List<object>();
            if (allowed.Contains(NetworkAccessService.ModuleFireSafety))
            {
                openWatches.AddRange((await watches.GetAllWithStatusAsync())
                    .Where(w => !w.IsArchived && w.Status == ListStatus.Open && w.EventDateTime >= DateTime.Now.Date)
                    .OrderBy(w => w.EventDateTime).Take(3)
                    .Select(w => new { id = w.Id, name = w.Name, time = w.EventDateTime, free = Math.Max(0, w.TotalRequired - w.TotalAssigned) }));
            }

            var openTasks = 0;
            if (m != null && allowed.Contains(NetworkAccessService.ModuleLeadership))
                openTasks = (await leadership.GetTasksAsync(LeadershipTaskStatus.Offen))
                    .Count(t => t.AssigneeMemberId == m.Id && info.CanSeeCircle(t.Circle));

            return Results.Json(new { events, watches = openWatches, openTasks });
        });

        // ------------------------------------------------- oeffentliche Ausweispruefung
        // Bewusst minimal: gueltig ja/nein + Lichtbild. Keine Namen, keine Nummern.
        app.MapGet("/client-api/card-check/{publicId}", async (string publicId, HttpContext http, MemberRepository members,
            NetworkAccessService access, SimpleRateLimiter limiter) =>
        {
            http.Response.Headers["X-Robots-Tag"] = "noindex";
            if (!limiter.Allow("card:" + access.GetClientIp(http), 30, TimeSpan.FromMinutes(1)))
                return Results.Json(new { error = "rate_limited" }, statusCode: 429);
            var m = await members.GetByCardPublicIdAsync(publicId);
            var valid = m != null && m.IsCardValid(DateTime.Now);
            return Results.Json(new { valid, checkedAt = DateTime.Now });
        });

        app.MapGet("/client-api/card-check/{publicId}/photo", async (string publicId, HttpContext http, MemberRepository members,
            DataPathService paths, NetworkAccessService access, SimpleRateLimiter limiter) =>
        {
            http.Response.Headers["X-Robots-Tag"] = "noindex";
            if (!limiter.Allow("card:" + access.GetClientIp(http), 30, TimeSpan.FromMinutes(1)))
                return Results.StatusCode(429);
            var m = await members.GetByCardPublicIdAsync(publicId);
            if (m == null || !m.IsCardValid(DateTime.Now)) return Results.NotFound();
            return PhotoResult(m, paths);
        });

        // ------------------------------------------------------------ Verwaltung
        var admin = app.MapGroup("/client-api/admin").RequireAuthorization("Admin").DisableAntiforgery();

        admin.MapGet("/members/{id:int}/account", async (int id, MemberRepository members, AppUrlService urls) =>
        {
            var m = await members.GetByIdAsync(id);
            if (m == null) return Results.NotFound();
            return Results.Json(new
            {
                id = m.Id,
                name = $"{m.FirstName} {m.LastName}",
                number = m.MemberNumber,
                hasPassword = !string.IsNullOrEmpty(m.PasswordHash),
                passwordSetAt = m.PasswordSetAt,
                modules = UserAccessService.ParseModules(m.ModuleAccess)?.ToArray() ?? Array.Empty<string>(),
                leadershipCircle = m.LeadershipCircle,
                rank = m.Rank,
                email = m.Email,
                hasPhoto = !string.IsNullOrEmpty(m.PhotoFile),
                photoVersion = m.PhotoUpdatedAt?.Ticks ?? 0,
                lockedUntil = m.LockedUntil,
                card = CardDto(m, urls)
            });
        });

        admin.MapPut("/members/{id:int}/account", async (int id, MemberRepository members, UserAccessService ua, LoginThrottle throttle, MemberAccountUpdate r) =>
        {
            var m = await members.GetByIdAsync(id);
            if (m == null) return Results.NotFound();
            m.ModuleAccess = UserAccessService.SerializeModules(false, r.Modules);
            m.LeadershipCircle = LeadershipCircles.IsValid(r.LeadershipCircle) ? r.LeadershipCircle : LeadershipCircles.None;
            m.Rank = string.IsNullOrWhiteSpace(r.Rank) ? null : r.Rank.Trim();
            m.Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim();
            m.CardValidUntil = r.CardValidUntil?.Date;
            if (r.Unlock) { m.LockedUntil = null; m.FailedLogins = 0; throttle.Unlock(m.MemberNumber); }
            await members.UpdateAsync(m);
            ua.Invalidate(UserAccessService.MemberPrincipalName(m.MemberNumber));
            return Results.Ok();
        });

        // Neu ausstellen: neue Kennung -> alter QR-Code/Link sofort ungueltig.
        admin.MapPost("/members/{id:int}/card/reissue", async (int id, MemberRepository members) =>
        {
            var m = await members.GetByIdAsync(id);
            if (m == null) return Results.NotFound();
            m.CardPublicId = CardIds.NewPublicId();
            m.CardRevokedAt = null;
            await members.UpdateAsync(m);
            return Results.Ok();
        });

        admin.MapPost("/members/{id:int}/card/revoke", async (int id, MemberRepository members) =>
        {
            var m = await members.GetByIdAsync(id);
            if (m == null) return Results.NotFound();
            m.CardRevokedAt = DateTime.Now;
            await members.UpdateAsync(m);
            return Results.Ok();
        });

        // Zugang entziehen: Passwort weg, laufende Sitzungen enden ueber UserAccessService.
        admin.MapDelete("/members/{id:int}/password", async (int id, MemberRepository members, UserAccessService ua) =>
        {
            var m = await members.GetByIdAsync(id);
            if (m == null) return Results.NotFound();
            m.PasswordHash = null;
            m.PasswordSetAt = null;
            await members.UpdateAsync(m);
            ua.Invalidate(UserAccessService.MemberPrincipalName(m.MemberNumber));
            return Results.Ok();
        });

        admin.MapGet("/members/{id:int}/photo", async (int id, MemberRepository members, DataPathService paths) =>
        {
            var m = await members.GetByIdAsync(id);
            return m == null ? Results.NotFound() : PhotoResult(m, paths);
        });

        admin.MapDelete("/members/{id:int}/photo", async (int id, MemberRepository members, DataPathService paths) =>
        {
            var m = await members.GetByIdAsync(id);
            if (m == null) return Results.NotFound();
            DeletePhotoFile(m, paths);
            m.PhotoFile = null;
            m.PhotoUpdatedAt = DateTime.Now;
            await members.UpdateAsync(m);
            return Results.Ok();
        });

        // ------------------------------------------- Passwort-Vergabe am Tablet
        // Nur Admin UND nur von einer Adresse der Login-Whitelist (Gerätehaus-Tablet).
        admin.MapGet("/enrollment", async (HttpContext http, NetworkAccessService access, PasswordEnrollmentService enroll, MemberRepository members) =>
        {
            var ip = access.GetClientIp(http)?.ToString() ?? "";
            var session = enroll.GetActive(http.User.Identity?.Name, access.EnrollmentClientKey(http));
            var today = (await members.GetAllAsync())
                .Where(m => m.PasswordSetAt?.Date == DateTime.Today && m.PasswordSetByUserId != null)
                .OrderByDescending(m => m.PasswordSetAt)
                .Select(m => new { name = $"{m.FirstName} {m.LastName}", number = m.MemberNumber, at = m.PasswordSetAt });
            return Results.Json(new
            {
                active = session != null,
                startedAt = session?.StartedAt,
                allowedDevice = access.IsEnrollmentDevice(http),
                deviceName = access.GetDevice(http)?.Name,
                deviceAllowsEnrollment = access.GetDevice(http)?.AllowEnrollment,
                clientIp = ip,
                today
            });
        });

        admin.MapPost("/enrollment/start", async (HttpContext http, NetworkAccessService access, PasswordEnrollmentService enroll, UserRepository users) =>
        {
            if (!access.IsEnrollmentDevice(http))
                return Results.Json(new { ok = false, error = "device" });
            var user = await users.GetByUsernameAsync(http.User.Identity?.Name ?? "");
            if (user == null) return Results.Forbid();
            enroll.Start(user.Username, user.Id, access.EnrollmentClientKey(http));
            return Results.Json(new { ok = true });
        });

        admin.MapPost("/enrollment/stop", (HttpContext http, PasswordEnrollmentService enroll) =>
        {
            enroll.Stop(http.User.Identity?.Name ?? "");
            return Results.Ok();
        });

        admin.MapPost("/enrollment/lookup", async (HttpContext http, NetworkAccessService access, PasswordEnrollmentService enroll,
            MemberRepository members, EnrollmentLookup r) =>
        {
            if (ActiveEnrollment(http, access, enroll) == null) return Results.Json(new { ok = false, error = "session" });
            var code = (r.Code ?? "").Trim();
            var m = code.Length == 0 ? null : await members.GetByMemberNumberAsync(code);
            if (m == null || !m.IsActive) return Results.Json(new { ok = false, error = "notfound" });
            return Results.Json(new
            {
                ok = true,
                member = new
                {
                    id = m.Id, name = $"{m.FirstName} {m.LastName}", number = m.MemberNumber, birthDate = m.BirthDate,
                    hasPhoto = !string.IsNullOrEmpty(m.PhotoFile), photoVersion = m.PhotoUpdatedAt?.Ticks ?? 0,
                    hasPassword = !string.IsNullOrEmpty(m.PasswordHash)
                }
            });
        });

        admin.MapPost("/enrollment/set", async (HttpContext http, NetworkAccessService access, PasswordEnrollmentService enroll,
            MemberRepository members, UserAccessService ua, EnrollmentSet r) =>
        {
            var session = ActiveEnrollment(http, access, enroll);
            if (session == null) return Results.Json(new { ok = false, error = "session" });
            var m = await members.GetByIdAsync(r.MemberId);
            if (m == null || !m.IsActive) return Results.Json(new { ok = false, error = "notfound" });
            if ((r.Password ?? "").Length < PasswordHasher.MinLength) return Results.Json(new { ok = false, error = "too_short" });

            m.PasswordHash = PasswordHasher.Hash(r.Password!);
            m.PasswordSetAt = DateTime.Now;
            m.PasswordSetByUserId = session.AdminUserId;
            m.FailedLogins = 0;
            m.LockedUntil = null;
            // Ausweis-Kennung beim ersten Zugang vergeben (gueltig wird er erst mit Lichtbild).
            m.CardPublicId ??= CardIds.NewPublicId();
            await members.UpdateAsync(m);
            ua.Invalidate(UserAccessService.MemberPrincipalName(m.MemberNumber));
            return Results.Json(new { ok = true });
        });
    }

    // ------------------------------------------------------------------ Helfer

    private static PasswordEnrollmentService.Session? ActiveEnrollment(HttpContext http, NetworkAccessService access, PasswordEnrollmentService enroll)
    {
        // Jede Aktion prueft erneut: zugelassenes Geraet, gleicher Client wie beim Start, nicht abgelaufen.
        if (!access.IsEnrollmentDevice(http)) return null;
        return enroll.GetActive(http.User.Identity?.Name, access.EnrollmentClientKey(http), touch: true);
    }

    /// <summary>Das angemeldete Mitglied (nur Mitglieder-Konten, nicht Admins/Benutzer).</summary>
    public static async Task<Member?> CurrentMemberAsync(HttpContext http, MemberRepository members, UserAccessService ua)
    {
        var info = ua.Get(http);
        if (info is not { IsMember: true, MemberId: int id }) return null;
        return await members.GetByIdAsync(id);
    }

    private static string CheckUrl(Member m, AppUrlService urls) => urls.BuildUrl("/ausweis/" + m.CardPublicId);

    private static object CardDto(Member m, AppUrlService urls) => new
    {
        issued = !string.IsNullOrEmpty(m.CardPublicId),
        valid = m.IsCardValid(DateTime.Now),
        validUntil = m.CardValidUntil,
        revoked = m.CardRevokedAt != null,
        hasPhoto = !string.IsNullOrEmpty(m.PhotoFile),
        checkUrl = string.IsNullOrEmpty(m.CardPublicId) ? null : CheckUrl(m, urls)
    };

    private static IResult PhotoResult(Member m, DataPathService paths)
    {
        var path = paths.PhotoPath(m.PhotoFile);
        if (path == null || !File.Exists(path)) return Results.NotFound();
        return Results.File(path, "image/jpeg");
    }

    private static void DeletePhotoFile(Member m, DataPathService paths)
    {
        var old = paths.PhotoPath(m.PhotoFile);
        try { if (old != null && File.Exists(old)) File.Delete(old); } catch { /* Altdatei bleibt notfalls liegen */ }
    }

    /// <summary>Speichert ein Lichtbild aus einer data:image/jpeg-URL. Rueckgabe: Fehlercode oder null.</summary>
    public static async Task<string?> SavePhotoAsync(Member m, string? dataUrl, MemberRepository members, DataPathService paths)
    {
        const string prefix = "data:image/jpeg;base64,";
        if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith(prefix, StringComparison.Ordinal)) return "format";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[prefix.Length..]); }
        catch (FormatException) { return "format"; }
        if (bytes.Length > MaxPhotoBytes) return "size";
        // JPEG-Signatur pruefen (FF D8 FF) - nur echte JPEGs speichern.
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF) return "format";

        var fileName = $"{m.Id}-{CardIds.NewPublicId()[..12]}.jpg";
        await File.WriteAllBytesAsync(Path.Combine(paths.PhotoDirectory, fileName), bytes);
        DeletePhotoFile(m, paths);
        m.PhotoFile = fileName;
        m.PhotoUpdatedAt = DateTime.Now;
        m.CardPublicId ??= CardIds.NewPublicId();
        await members.UpdateAsync(m);
        return null;
    }
}

public record PhotoUpload(string? DataUrl);
public record ProfileUpdate(string? Email);
public record MemberAccountUpdate(List<string>? Modules, int LeadershipCircle, string? Rank, string? Email, DateTime? CardValidUntil, bool Unlock = false);
public record EnrollmentLookup(string? Code);
public record EnrollmentSet(int MemberId, string? Password);
