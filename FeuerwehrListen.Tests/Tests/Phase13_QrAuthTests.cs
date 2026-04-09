namespace FeuerwehrListen.Tests.Tests;

[TestFixture, Order(13)]
public class Phase13_QrAuthTests : BaseTest
{
    [Test, Order(1)]
    public async Task AdminUsers_EditButtonVisible()
    {
        await LoginAsAdmin();
        await GoTo("/admin/users");
        await Assertions.Expect(Page.GetByText("Bearbeiten").First).ToBeVisibleAsync();
    }

    [Test, Order(2)]
    public async Task AdminUsers_EditForm_ShowsQrAuthCodeField()
    {
        await LoginAsAdmin();
        await GoTo("/admin/users");

        // Click edit on admin user
        await Page.GetByText("Bearbeiten").First.ClickAsync();
        await WaitForBlazor();

        // QR-Auth-Code field should be visible
        await Assertions.Expect(Page.GetByText("QR-Auth-Code").First).ToBeVisibleAsync();
    }

    [Test, Order(3)]
    public async Task AdminUsers_CanSetQrCodeManually()
    {
        await LoginAsAdmin();
        await GoTo("/admin/users");

        await Page.GetByText("Bearbeiten").First.ClickAsync();
        await WaitForBlazor();

        // Enter QR code manually via text input
        var qrInput = Page.Locator("input[placeholder*='QR-Code scannen']").First;
        await qrInput.FillAsync("test-qr-hash-12345");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).First.ClickAsync();
        await WaitForBlazor();

        // Edit again to verify it was saved
        await Page.GetByText("Bearbeiten").First.ClickAsync();
        await WaitForBlazor();

        var qrInput2 = Page.Locator("input[placeholder*='QR-Code scannen']").First;
        var value = await qrInput2.InputValueAsync();
        Assert.That(value, Is.EqualTo("test-qr-hash-12345"));
    }

    [Test, Order(4)]
    public async Task Login_ShowsQrLoginButton()
    {
        await GoTo("/login");
        await Assertions.Expect(Page.GetByText("Login per QR-Code").First).ToBeVisibleAsync();
    }

    [Test, Order(5)]
    public async Task Login_ToggleQrLogin_ShowsScanner()
    {
        await GoTo("/login");
        await Page.GetByText("Login per QR-Code").First.ClickAsync();
        await WaitForBlazor();

        // QR Scanner card should appear
        await Assertions.Expect(Page.Locator(".qr-scanner-inline").First).ToBeVisibleAsync();
    }

    [Test, Order(6)]
    public async Task OperationEditEntries_PageAccessible()
    {
        await LoginAsAdmin();
        // Create operation list first
        await GoTo("/create-operation");
        await FillFieldAfterLabel("Einsatznummer", "EDIT-TEST");
        await FillFieldAfterLabel("Stichwort", "B1");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Liste erstellen" }).ClickAsync();
        await WaitForBlazor();

        // Navigate to edit-entries page
        var url = Page.Url;
        var listIdMatch = System.Text.RegularExpressions.Regex.Match(url, @"/operation/(\d+)");
        if (listIdMatch.Success)
        {
            var listId = listIdMatch.Groups[1].Value;
            await GoTo($"/operation/{listId}/edit-entries");
            await Assertions.Expect(Page.Locator("h1")).ToContainTextAsync("Einträge bearbeiten");
        }
    }
}
