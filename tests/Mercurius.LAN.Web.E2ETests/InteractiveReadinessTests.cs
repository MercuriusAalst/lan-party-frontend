using Microsoft.Playwright;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Guards the interactivity gate used by every page-object wait.
///
/// Blazor opens its circuit websocket before it applies the interactive render, and a click or
/// keystroke dispatched in that window is dropped because no DOM handler is bound yet. The gate in
/// <see cref="E2EPageExtensions.WaitForInteractiveAsync"/> therefore also waits for the client to
/// replace the prerendered interactive-root placeholder. A gate that silently passes too early is
/// invisible in the suite until a cold CI runner turns it into a flake, so pin the signal itself.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class InteractiveReadinessTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task ReadinessGateStaysClosedUntilTheCircuitAppliesItsRender()
    {
        await using var blockedContext = await app.NewContextAsync();
        var blockedPage = await blockedContext.NewPageAsync();
        // Aborting the circuit request leaves the server-rendered shell in place with its
        // interactive-root placeholder, which is exactly the pre-activation state the gate must
        // reject. If this ever stops blocking, the assertion fails loudly instead of passing.
        await blockedPage.RouteAsync("**/_blazor**", route => route.AbortAsync());
        await blockedPage.GotoAsync($"{app.BaseUrl}");

        Assert.False(
            await blockedPage.IsInteractiveRootAppliedAsync(),
            "The server-rendered shell alone must not be reported as interactive.");

        await using var liveContext = await app.NewContextAsync();
        var livePage = await liveContext.NewPageAsync();
        await livePage.GotoAsync($"{app.BaseUrl}");
        await livePage.WaitForInteractiveAsync();

        Assert.True(
            await livePage.IsInteractiveRootAppliedAsync(),
            "The live circuit must have applied its interactive render before the wait returns.");
    }

    [Fact]
    public async Task ShippedShellAcceptsTheFirstInteractiveClickAfterWaiting()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();

        // A single click must land on a bound handler: no retry, no extra settling.
        await page.Locator(".theme-toggle").ClickAsync();

        await Assertions.Expect(page.Locator(".layout-shell")).ToHaveAttributeAsync("data-theme-state", "dark");
    }
}
