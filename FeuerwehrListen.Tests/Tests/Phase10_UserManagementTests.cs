using Microsoft.Playwright;

namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(10)]
public class Phase10_UserManagementTests : BaseTest
{
    [SetUp]
    public async Task Login()
    {
        await LoginAsAdmin();
    }

    [Test, Order(1)]
    public async Task Users_PageLoads()
    {
        await GoTo("/admin/users");
        await Assertions.Expect(Page.Locator("h1")).ToContainTextAsync("Benutzer");
    }

    [Test, Order(2)]
    public async Task ApiKeys_PageLoads()
    {
        await GoTo("/admin/apikeys");
        await Assertions.Expect(Page.Locator("h1")).ToContainTextAsync("API");
    }

    [Test, Order(3)]
    public async Task ScheduledLists_PageLoads()
    {
        await GoTo("/admin/scheduled");
        await Assertions.Expect(Page.Locator("h1")).ToContainTextAsync("Geplante");
    }

    [Test, Order(4)]
    public async Task ServiceStatus_PageLoads()
    {
        await GoTo("/admin/service-status");
        await Assertions.Expect(Page.Locator("h1")).ToContainTextAsync("Service");
    }

    [Test, Order(5)]
    public async Task ChangePassword_PageLoads()
    {
        await GoTo("/change-password");
        await Assertions.Expect(Page.Locator("h3")).ToContainTextAsync("Passwort");
    }
}
