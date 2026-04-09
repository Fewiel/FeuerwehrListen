namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(1)]
public class Phase1_StartupLoginTests : BaseTest
{
    [Test, Order(1)]
    public async Task StartPage_ShowsOffeneListenEmpty()
    {
        await GoTo("/");
        await AssertTextVisible("Offene Listen");
        await AssertTextVisible("Keine offenen Einsatzlisten");
    }

    [Test, Order(2)]
    public async Task Login_WithAdminCredentials_Succeeds()
    {
        await LoginAsAdmin();
        // Admin sidebar should be visible
        await AssertTextVisible("Admin-Bereich");
        await AssertTextVisible("Mitglieder");
        await AssertTextVisible("Einstellungen");
    }

    [Test, Order(3)]
    public async Task Login_WithWrongPassword_Fails()
    {
        await GoTo("/login");
        await Page.Locator("input[type='text']").First.FillAsync("admin");
        await Page.Locator("input[type='password']").First.FillAsync("wrongpassword");
        await Page.Locator("button.btn-primary").First.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        // Should show error or stay on login page
        var url = Page.Url;
        Assert.That(url, Does.Contain("/login"));
    }

    [Test, Order(4)]
    public async Task NavLinks_AllModulesVisible()
    {
        await LoginAsAdmin();
        await AssertTextVisible("Neue Anwesenheitsliste");
        await AssertTextVisible("Neue Einsatzliste");
        await AssertTextVisible("Brandsicherheitswachen");
    }
}
