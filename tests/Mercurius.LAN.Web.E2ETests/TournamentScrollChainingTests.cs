using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class TournamentScrollChainingTests(PlaywrightE2EFixture app) : TournamentE2ETestBase(app)
{
    [Theory]
    [InlineData(1440, 900)]
    [InlineData(900, 900)]
    [InlineData(390, 844)]
    public async Task EmptyParticipantAndRegistrationCardsScrollThePage(int width, int height)
    {
        var admin = await App.CreatePersonaAsync($"scroll-empty-admin-{width}", isAdmin: true);
        var tournamentId = await CreateIndividualTournamentAsync(
            App.CreateApiClient(admin), TournamentE2E.Unique($"E2E scroll empty {width}"));

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await page.WaitForInteractiveAsync();

        var emptyParticipants = page.Locator(".participants-state-card");
        var registration = page.Locator("#registration");
        await Expect(emptyParticipants).ToContainTextAsync("No participants yet");
        await Expect(registration).ToBeVisibleAsync();

        await AssertPageWheelChainsAsync(page, emptyParticipants);
        await AssertPageWheelChainsAsync(page, registration);
        await CaptureAsync(page, $"empty-{width}.png");
        await MatchE2E.CloseContextsAsync(App, context);
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(900, 900)]
    [InlineData(390, 844)]
    public async Task PopulatedParticipantAndScheduleListsScrollInsideThenChainToPage(int width, int height)
    {
        var admin = await App.CreatePersonaAsync($"scroll-populated-admin-{width}", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var participants = await MatchE2E.CreatePersonasAsync(App, $"scroll-player-{width}", 12);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            App, adminApi, participants, "DoubleElimination");

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await page.WaitForInteractiveAsync();

        var participantList = page.Locator(".participants-overview-body");
        var scheduleList = page.Locator(".Tournament-schedule-list");
        await Expect(page.Locator(".participant-overview-card")).ToHaveCountAsync(participants.Count);
        Assert.True(await scheduleList.Locator(".Tournament-schedule-item").CountAsync() > 3);

        if (width > 1100)
            await AssertScrollRegionAsync(page, participantList);
        else
            await AssertPageWheelChainsAsync(page, participantList);

        if (width > 640)
            await AssertScrollRegionAsync(page, scheduleList);
        else
            await AssertPageWheelChainsAsync(page, scheduleList);

        await CaptureAsync(page, $"populated-{width}.png");
        await MatchE2E.CloseContextsAsync(App, context);
    }

    private static async Task AssertScrollRegionAsync(IPage page, ILocator scrollRegion)
    {
        await scrollRegion.ScrollIntoViewIfNeededAsync();
        Assert.True(
            await scrollRegion.EvaluateAsync<bool>("element => element.scrollHeight > element.clientHeight"),
            $"{scrollRegion} must overflow internally at this width.");

        await scrollRegion.EvaluateAsync("element => { element.scrollTop = 0; }");
        await HoverInsideAsync(page, scrollRegion);
        await page.Mouse.WheelAsync(0, 120);
        await page.WaitForFunctionAsync(
            "element => element.scrollTop > 0",
            await scrollRegion.ElementHandleAsync(),
            new() { Timeout = 3000 });

        await scrollRegion.EvaluateAsync("element => { element.scrollTop = element.scrollHeight; }");
        await HoverInsideAsync(page, scrollRegion);
        var pageScrollY = await GetPageScrollYAsync(page);
        await page.Mouse.WheelAsync(0, 500);
        await page.WaitForFunctionAsync(
            "before => window.scrollY > before",
            pageScrollY,
            new() { Timeout = 3000 });
    }

    private static async Task AssertPageWheelChainsAsync(IPage page, ILocator target)
    {
        await page.EvaluateAsync("() => window.scrollTo(0, 0)");
        await target.ScrollIntoViewIfNeededAsync();
        await HoverInsideAsync(page, target);

        var before = await GetPageScrollYAsync(page);
        var maximum = await page.EvaluateAsync<double>(
            "() => document.documentElement.scrollHeight - window.innerHeight");
        var scrollDown = maximum - before > 200;
        await page.Mouse.WheelAsync(0, scrollDown ? 500 : -500);
        await page.WaitForFunctionAsync(
            scrollDown ? "before => window.scrollY > before" : "before => window.scrollY < before",
            before,
            new() { Timeout = 3000 });
    }

    private static async Task HoverInsideAsync(IPage page, ILocator target)
    {
        var box = await target.BoundingBoxAsync()
            ?? throw new InvalidOperationException($"Cannot hover {target} because it has no bounding box.");
        var viewport = await page.EvaluateAsync<int[]>("() => [window.innerWidth, window.innerHeight]");
        var left = Math.Max(0, box.X);
        var top = Math.Max(0, box.Y);
        var right = Math.Min(viewport[0], box.X + box.Width);
        var bottom = Math.Min(viewport[1], box.Y + box.Height);
        Assert.True(right > left && bottom > top, $"{target} is outside the viewport.");
        await page.Mouse.MoveAsync((left + right) / 2, (top + bottom) / 2);
    }

    private static Task<double> GetPageScrollYAsync(IPage page) =>
        page.EvaluateAsync<double>("() => window.scrollY");

    private async Task CaptureAsync(IPage page, string fileName)
    {
        var directory = Path.Combine(App.ArtifactDirectory, "scroll-chaining");
        Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(directory, fileName),
            FullPage = true
        });
    }
}
