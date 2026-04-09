namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(7)]
public class Phase7_DefectTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task DefectList_PageLoads()
    {
        await GoTo("/defects");
        await Assertions.Expect(Page.Locator("h1")).ToContainTextAsync("Mängel");
    }
}
