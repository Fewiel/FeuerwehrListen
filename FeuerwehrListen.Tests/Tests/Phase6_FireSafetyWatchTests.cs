namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(6)]
public class Phase6_FireSafetyWatchTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task FireSafetyWatch_ListPage()
    {
        await GoTo("/firesafetywatches");
        await AssertTextVisible("Brandsicherheitswachen");
    }

    [Test, Order(2)]
    public async Task CreateFireSafetyWatch()
    {
        await GoTo("/firesafetywatches/create");
        await FillFieldAfterLabel("Name / Anlass", "Stadtfest");
        await FillFieldAfterLabel("Ort", "Marktplatz");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Brandsicherheitswache anlegen" }).ClickAsync();
        await WaitForBlazor();
        await AssertTextVisible("Stadtfest");
    }
}
