namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(4)]
public class Phase4_AttendanceTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task CreateAttendanceList()
    {
        await GoTo("/create-attendance");
        await FillFieldAfterLabel("Titel", "Dienstabend Test");
        await FillFieldAfterLabel("Einheit", "Löschzug 1");

        // Select unit number dropdown
        var unitSelect = Page.Locator("select.form-select").First;
        await unitSelect.SelectOptionAsync("1");

        await FillFieldAfterLabel("Beschreibung", "Testübung");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Erstellen" }).ClickAsync();
        await WaitForBlazor();
        await AssertTextVisible("Dienstabend Test");
    }

    [Test, Order(2)]
    public async Task AddMemberByNumpad()
    {
        await GoTo("/");
        await Page.GetByText("Dienstabend Test").First.ClickAsync();
        await WaitForBlazor();

        await Page.GetByText("Ohne QR-Code eintragen").First.ClickAsync();
        await WaitForBlazor();

        foreach (var d in "1001")
            await Page.Locator($".numpad-btn:has-text('{d}')").ClickAsync();
        await Page.Locator(".numpad-btn-submit").ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazor();

        await AssertTextVisible("Max Mustermann");
    }

    [Test, Order(3)]
    public async Task CreateSecondListForUnitRouting()
    {
        await GoTo("/create-attendance");
        await FillFieldAfterLabel("Titel", "Dienstabend E2");
        await FillFieldAfterLabel("Einheit", "Löschzug 2");
        var unitSelect = Page.Locator("select.form-select").First;
        await unitSelect.SelectOptionAsync("2");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Erstellen" }).ClickAsync();
        await WaitForBlazor();
        await AssertTextVisible("Dienstabend E2");
    }

    [Test, Order(4)]
    public async Task UnitRouting_AutoRedirect()
    {
        await GoTo("/");
        await Page.GetByText("Dienstabend Test").First.ClickAsync();
        await WaitForBlazor();

        await Page.GetByText("Ohne QR-Code eintragen").First.ClickAsync();
        await WaitForBlazor();
        foreach (var d in "2040")
            await Page.Locator($".numpad-btn:has-text('{d}')").ClickAsync();
        await Page.Locator(".numpad-btn-submit").ClickAsync();
        await Page.WaitForTimeoutAsync(3000);
        await WaitForBlazor();

        // Should have been redirected to E2 and Peter visible
        await AssertTextVisible("Peter Weitkamp");
    }

    [Test, Order(5)]
    public async Task CloseList()
    {
        await GoTo("/");
        await Page.GetByText("Dienstabend Test").First.ClickAsync();
        await WaitForBlazor();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Liste abschließen" }).ClickAsync();
        await WaitForBlazor();
        await AssertTextVisible("Abgeschlossen");
    }
}
