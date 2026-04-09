namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(8)]
public class Phase8_ArchiveTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task ClosedLists_ShowsClosedLists()
    {
        await GoTo("/admin/closed-lists");
        await AssertTextVisible("Abgeschlossene Listen");
    }

    [Test, Order(2)]
    public async Task Archive_PageLoads()
    {
        await GoTo("/admin/archive");
        await AssertTextVisible("Archiv");
    }
}
