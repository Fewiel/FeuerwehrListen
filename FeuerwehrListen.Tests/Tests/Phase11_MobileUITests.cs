using Microsoft.Playwright;

namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(11)]
public class Phase11_MobileUITests : BaseTest
{
    [SetUp]
    public new async Task BaseSetUp()
    {
        Context = await GlobalSetup.Browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 375, Height = 812 }
        });
        Page = await Context.NewPageAsync();
    }

    [Test, Order(1)]
    public async Task Mobile_TopbarVisible()
    {
        await GoTo("/");
        await Assertions.Expect(Page.Locator(".mobile-topbar")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".mobile-topbar-title")).ToBeVisibleAsync();
    }

    [Test, Order(2)]
    public async Task Mobile_HamburgerOpensMenu()
    {
        await GoTo("/");
        await Page.ClickAsync(".mobile-topbar button");
        await WaitForBlazor();
        // Sidebar should show nav items
        await Assertions.Expect(Page.Locator(".sidebar a[href='']")).ToBeVisibleAsync();
    }

    [Test, Order(3)]
    public async Task Mobile_BackdropClosesMenu()
    {
        await GoTo("/");
        await Page.ClickAsync(".mobile-topbar button");
        await WaitForBlazor();

        // Click backdrop with force to bypass sidebar overlay
        var backdrop = Page.Locator(".mobile-sidebar-backdrop");
        if (await backdrop.IsVisibleAsync())
        {
            await backdrop.ClickAsync(new() { Force = true });
            await WaitForBlazor();
        }

        var pageHasClass = await Page.EvaluateAsync<bool>(
            "document.querySelector('.page')?.classList.contains('mobile-sidebar-open') ?? false");
        Assert.That(pageHasClass, Is.False);
    }
}
