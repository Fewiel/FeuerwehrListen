namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(12)]
public class Phase12_TabletUXTests : BaseTest
{
    [Test, Order(1)]
    public async Task ReconnectBanner_ExistsInDOM()
    {
        await GoTo("/");
        var banner = Page.Locator("#reconnect-banner");
        // Banner should exist but be hidden
        await Assertions.Expect(banner).ToHaveCountAsync(1);
        var cls = await banner.GetAttributeAsync("class");
        Assert.That(cls, Does.Contain("reconnect-hidden"));
    }

    [Test, Order(2)]
    public async Task PWA_ManifestAccessible()
    {
        var response = await Page.APIRequest.GetAsync($"{BaseUrl}/manifest.json");
        Assert.That(response.Status, Is.EqualTo(200));
        var text = await response.TextAsync();
        Assert.That(text, Does.Contain("Feuerwehr Listen"));
    }

    [Test, Order(3)]
    public async Task PWA_ServiceWorkerAccessible()
    {
        var response = await Page.APIRequest.GetAsync($"{BaseUrl}/service-worker.js");
        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test, Order(4)]
    public async Task PWA_ManifestLinkedInHead()
    {
        await GoTo("/");
        var manifest = Page.Locator("link[rel='manifest']");
        await Assertions.Expect(manifest).ToHaveCountAsync(1);
    }

    [Test, Order(5)]
    public async Task PWA_ThemeColorSet()
    {
        await GoTo("/");
        var meta = Page.Locator("meta[name='theme-color']");
        await Assertions.Expect(meta).ToHaveCountAsync(1);
        var color = await meta.GetAttributeAsync("content");
        Assert.That(color, Is.EqualTo("#0d1117"));
    }

    [Test, Order(6)]
    public async Task ManualEntryButton_VisibleOnAttendance()
    {
        await LoginAsAdmin();
        await GoTo("/create-attendance");
        await FillFieldAfterLabel("Titel", "ManualEntry Test");
        await FillFieldAfterLabel("Einheit", "Test");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Erstellen" }).ClickAsync();
        await WaitForBlazor();

        // "Ohne QR-Code eintragen" button should be visible
        await Assertions.Expect(Page.GetByText("Ohne QR-Code eintragen").First).ToBeVisibleAsync();
    }

    [Test, Order(7)]
    public async Task DuplicateEntry_ShowsError()
    {
        await LoginAsAdmin();

        // Create a list and add a member
        await GoTo("/create-attendance");
        await FillFieldAfterLabel("Titel", "Duplikat Test");
        await FillFieldAfterLabel("Einheit", "Test");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Erstellen" }).ClickAsync();
        await WaitForBlazor();

        // Enter member 1001
        await Page.GetByText("Ohne QR-Code eintragen").First.ClickAsync();
        await WaitForBlazor();
        foreach (var d in "1001")
            await Page.Locator($".numpad-btn:has-text('{d}')").ClickAsync();
        await Page.Locator(".numpad-btn-submit").ClickAsync();
        await Page.WaitForTimeoutAsync(3000);

        // Try to enter same member again
        await Page.GetByText("Ohne QR-Code eintragen").First.ClickAsync();
        await WaitForBlazor();
        foreach (var d in "1001")
            await Page.Locator($".numpad-btn:has-text('{d}')").ClickAsync();
        await Page.Locator(".numpad-btn-submit").ClickAsync();
        await Page.WaitForTimeoutAsync(3000);

        // Should show duplicate error
        await AssertTextVisible("bereits eingetragen");
    }

    [Test, Order(8)]
    public async Task SoundFiles_Accessible()
    {
        var successResp = await Page.APIRequest.GetAsync($"{BaseUrl}/sounds/success.wav");
        Assert.That(successResp.Status, Is.EqualTo(200));

        var errorResp = await Page.APIRequest.GetAsync($"{BaseUrl}/sounds/error.wav");
        Assert.That(errorResp.Status, Is.EqualTo(200));
    }
}
