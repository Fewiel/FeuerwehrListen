namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(9)]
public class Phase9_StatisticsTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task Statistics_PageLoads()
    {
        await GoTo("/admin/statistics");
        await Assertions.Expect(Page.Locator("h1")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
