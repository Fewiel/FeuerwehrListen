using Microsoft.Playwright;

namespace FeuerwehrListen.Tests;

/// <summary>
/// Base class for all Playwright E2E tests.
/// Creates a fresh browser context per test (isolated cookies/storage).
/// Provides helper methods for common UI operations.
/// </summary>
public abstract class BaseTest
{
    protected IBrowserContext Context = null!;
    protected IPage Page = null!;
    protected string BaseUrl => GlobalSetup.Server.BaseUrl;

    [SetUp]
    public async Task BaseSetUp()
    {
        Context = await GlobalSetup.Browser.NewContextAsync();
        Page = await Context.NewPageAsync();
    }

    [TearDown]
    public async Task BaseTearDown()
    {
        await Context.DisposeAsync();
    }

    // --- Navigation Helpers ---

    protected async Task GoTo(string path)
    {
        await Page.GotoAsync($"{BaseUrl}{path}");
        await WaitForBlazor();
    }

    protected async Task WaitForBlazor()
    {
        // Wait for page to finish loading and Blazor to hydrate
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle,
            new() { Timeout = 15000 });
        await Page.WaitForTimeoutAsync(1000);
    }

    // --- Auth Helpers ---

    protected async Task LoginAsAdmin()
    {
        await GoTo("/login");
        await Page.Locator("input[type='text']").First.FillAsync("admin");
        await Page.Locator("input[type='password']").First.FillAsync("admin");
        await Page.Locator("button.btn-primary").First.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazor();
    }

    // --- Stammdaten Helpers ---

    protected async Task CreateVehicle(string name, string callSign, string type)
    {
        await GoTo("/admin/vehicles");
        await Page.ClickAsync("text=Neues Fahrzeug");
        await WaitForBlazor();
        await Page.FillAsync("input >> nth=0", name);
        await Page.FillAsync("input >> nth=1", callSign);
        await Page.SelectOptionAsync("select", type);
        await Page.ClickAsync("text=Speichern");
        await WaitForBlazor();
    }

    protected async Task CreateFunction(string name, bool isDefault = false)
    {
        await GoTo("/admin/functions");
        await Page.ClickAsync("text=Neue Funktion");
        await WaitForBlazor();
        await Page.FillAsync("input[type='text']", name);
        if (isDefault)
            await Page.CheckAsync("input[type='checkbox']");
        await Page.ClickAsync("text=Speichern");
        await WaitForBlazor();
    }

    protected async Task CreateMemberManual(string number, string firstName, string lastName, int? unit = null)
    {
        await GoTo("/admin/members");
        await Page.GetByText("Neues Mitglied").First.ClickAsync();
        await WaitForBlazor();

        // Fill fields by finding input after label text
        await FillFieldAfterLabel("Mitgliedsnummer", number);
        await FillFieldAfterLabel("Vorname", firstName);
        await FillFieldAfterLabel("Nachname", lastName);

        if (unit.HasValue)
        {
            var select = Page.Locator(".card-body select.form-select").First;
            await select.SelectOptionAsync(unit.Value.ToString());
        }

        await Page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await WaitForBlazor();
    }

    /// <summary>Finds input/textarea/select following a label containing the given text.</summary>
    protected async Task FillFieldAfterLabel(string labelText, string value)
    {
        // Find the parent .mb-3 div that contains the label, then fill its input
        var container = Page.Locator($".mb-3:has(label:has-text('{labelText}'))").First;
        var input = container.Locator("input, textarea, select").First;
        await input.FillAsync(value);
    }

    protected async Task EnterMemberByNumpad(string memberNumber)
    {
        // Click the "Eintragen" button
        await Page.ClickAsync("text=Eintragen");
        await WaitForBlazor();

        // Type each digit on the numpad
        foreach (var digit in memberNumber)
        {
            await Page.ClickAsync($".numpad-btn >> text='{digit}'");
        }

        // Click the green checkmark (submit)
        await Page.ClickAsync(".numpad-btn-submit");
        await WaitForBlazor();
    }

    // --- Assertion Helpers ---

    protected async Task AssertTextVisible(string text, int timeout = 5000)
    {
        await Assertions.Expect(Page.GetByText(text).First).ToBeVisibleAsync(
            new() { Timeout = timeout });
    }

    protected async Task AssertTextNotVisible(string text, int timeout = 3000)
    {
        await Assertions.Expect(Page.GetByText(text).First).Not.ToBeVisibleAsync(
            new() { Timeout = timeout });
    }

    protected async Task<int> CountTableRows(string tableSelector = "table tbody tr")
    {
        return await Page.Locator(tableSelector).CountAsync();
    }
}
