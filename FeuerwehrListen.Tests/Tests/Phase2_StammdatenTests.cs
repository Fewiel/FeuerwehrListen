namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(2)]
public class Phase2_StammdatenTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task Vehicles_CreateFive()
    {
        var vehicles = new[]
        {
            ("LF 10", "Florian Billerbeck 1-43-1", "LF"),
            ("TLF 3000", "Florian Billerbeck 1-23-1", "TLF"),
            ("DLK 23/12", "Florian Billerbeck 1-33-1", "DLK"),
            ("MTW", "Florian Billerbeck 1-19-1", "MTW"),
            ("KdoW", "Florian Billerbeck 1-11-1", "KdoW"),
        };

        foreach (var (name, callSign, type) in vehicles)
        {
            await CreateVehicle(name, callSign, type);
        }

        await GoTo("/admin/vehicles");
        await AssertTextVisible("LF 10");
        await AssertTextVisible("TLF 3000");
        await AssertTextVisible("DLK 23/12");
        await AssertTextVisible("MTW");
        await AssertTextVisible("KdoW");
    }

    [Test, Order(2)]
    public async Task Functions_CreateFive()
    {
        var functions = new[]
        {
            ("Maschinist", false),
            ("Atemschutzgeräteträger", false),
            ("Gruppenführer", false),
            ("Zugführer", false),
            ("Melder", true),
        };

        foreach (var (name, isDefault) in functions)
        {
            await CreateFunction(name, isDefault);
        }

        await GoTo("/admin/functions");
        await AssertTextVisible("Maschinist");
        await AssertTextVisible("Atemschutzgeräteträger");
        await AssertTextVisible("Melder");
    }

    [Test, Order(3)]
    public async Task Keywords_CreateFour()
    {
        await GoTo("/admin/keywords");

        var keywords = new[] { "B1", "B2", "TH", "Öl1" };
        foreach (var kw in keywords)
        {
            await Page.ClickAsync("text=Neues Stichwort");
            await WaitForBlazor();
            await Page.FillAsync("input[type='text'] >> nth=0", kw);
            await Page.ClickAsync("text=Speichern");
            await WaitForBlazor();
        }

        await AssertTextVisible("B1");
        await AssertTextVisible("B2");
        await AssertTextVisible("TH");
    }

    [Test, Order(4)]
    public async Task Members_CreateManually()
    {
        await CreateMemberManual("1001", "Max", "Mustermann", 1);
        await CreateMemberManual("1002", "Anna", "Schmidt", 1);
        await CreateMemberManual("2040", "Peter", "Weitkamp", 2);

        await GoTo("/admin/members");
        await AssertTextVisible("Max");
        await AssertTextVisible("Anna");
        await AssertTextVisible("Peter");
        await AssertTextVisible("Einheit 1");
        await AssertTextVisible("Einheit 2");
    }

    [Test, Order(5)]
    public async Task Members_CsvImport()
    {
        await GoTo("/admin/members");
        await Page.ClickAsync("text=CSV Import");
        await WaitForBlazor();

        var csvPath = Path.Combine(AppContext.BaseDirectory, "TestData", "test_members.csv");
        await Page.SetInputFilesAsync("input[type='file']", csvPath);
        await WaitForBlazor();
        await Page.WaitForTimeoutAsync(3000); // Wait for import

        await AssertTextVisible("importiert");
        await AssertTextVisible("Klaus");
        await AssertTextVisible("Sabine");
    }

    [Test, Order(6)]
    public async Task Members_CsvImportDuplicates_Skipped()
    {
        await GoTo("/admin/members");
        await Page.ClickAsync("text=CSV Import");
        await WaitForBlazor();

        var csvPath = Path.Combine(AppContext.BaseDirectory, "TestData", "test_members.csv");
        await Page.SetInputFilesAsync("input[type='file']", csvPath);
        await WaitForBlazor();
        await Page.WaitForTimeoutAsync(3000);

        await AssertTextVisible("übersprungen");
    }

    [Test, Order(7)]
    public async Task Members_EditUnit()
    {
        await GoTo("/admin/members");
        // Click edit on Peter Weitkamp
        var peterRow = Page.Locator("tr", new() { HasText = "Weitkamp" });
        await peterRow.Locator("text=Bearbeiten").ClickAsync();
        await WaitForBlazor();

        // Change unit to 1
        await Page.SelectOptionAsync("select", "1");
        await Page.ClickAsync("text=Speichern");
        await WaitForBlazor();

        // Verify badge changed
        var peterRow2 = Page.Locator("tr", new() { HasText = "Weitkamp" });
        await Microsoft.Playwright.Assertions.Expect(peterRow2.Locator("text=Einheit 1")).ToBeVisibleAsync();

        // Change back to 2
        await peterRow2.Locator("text=Bearbeiten").ClickAsync();
        await WaitForBlazor();
        await Page.SelectOptionAsync("select", "2");
        await Page.ClickAsync("text=Speichern");
        await WaitForBlazor();
    }
}
