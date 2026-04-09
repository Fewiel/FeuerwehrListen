namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(5)]
public class Phase5_OperationTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task CreateOperationList()
    {
        await GoTo("/create-operation");
        await FillFieldAfterLabel("Einsatznummer", "123456");
        // Stichwort is an input with datalist, not a select
        await FillFieldAfterLabel("Stichwort", "B2");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Liste erstellen" }).ClickAsync();
        await WaitForBlazor();
        await AssertTextVisible("123456");
    }

    [Test, Order(2)]
    public async Task AddEntryWithVehicle()
    {
        await GoTo("/");
        await Page.GetByText("123456").First.ClickAsync();
        await WaitForBlazor();

        await Page.GetByText("Ohne QR-Code eintragen").First.ClickAsync();
        await WaitForBlazor();
        foreach (var d in "1001")
            await Page.Locator($".numpad-btn:has-text('{d}')").ClickAsync();
        await Page.Locator(".numpad-btn-submit").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Page.GetByText("LF 10").First.ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Page.GetByText("Maschinist").First.ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Bestätigen" }).ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        await WaitForBlazor();

        // Entry may be below viewport — check page content
        var content = await Page.ContentAsync();
        Assert.That(content, Does.Contain("Max Mustermann"));
    }

    [Test, Order(3)]
    public async Task CloseOperationList()
    {
        await GoTo("/");
        await Page.GetByText("123456").First.ClickAsync();
        await WaitForBlazor();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Liste abschließen" }).ClickAsync();
        await WaitForBlazor();
        await AssertTextVisible("Abgeschlossen");
    }
}
