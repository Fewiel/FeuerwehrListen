using Microsoft.Playwright;

namespace FeuerwehrListen.Tests;

/// <summary>
/// Global setup/teardown for the entire test run.
/// Starts the server once and shares it across all test classes.
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    public static TestServerFixture Server { get; private set; } = new();
    public static IPlaywright PlaywrightInstance { get; private set; } = null!;
    public static IBrowser Browser { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        Server.Start();
        PlaywrightInstance = await Playwright.CreateAsync();
        Browser = await PlaywrightInstance.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await Browser.DisposeAsync();
        PlaywrightInstance.Dispose();
        Server.Dispose();
    }
}
