using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The assigned admin's in-app queue of matches that need a decision: the nav count, the
/// MatchResolutions page and resolving through the existing match details dialog. The backend
/// persists expired deadlines from a background processor, so the tests wait (bounded) for the
/// match to appear in the admin API list before asserting on the UI.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchResolutionQueueTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    private static readonly TimeSpan ProcessorTimeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task AdminSeesTheMatchWithAMissingOpponentReportAndResolvesIt()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("queue-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "queue-player", 2);
        var adminApi = app.CreateApiClient(admin);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, adminApi, players, format: "BestOf3");
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[0], 2, 1);
        await MatchE2E.ExpireScoreWindowAsync(app, matchId);
        await WaitForPendingResolutionAsync(adminApi, matchId);

        var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, "admin/match-resolutions"));
        await page.WaitForInteractiveAsync();

        var item = page.Locator($"li.match-resolution-item[data-match-id='{matchId}']");
        await Expect(item).ToBeVisibleAsync();
        await Expect(item).ToContainTextAsync(players[0].Username);
        await Expect(item).ToContainTextAsync(players[1].Username);
        await Expect(item).ToContainTextAsync("The opponent did not report a score before the window closed.");
        await Expect(page.Locator("#admin-nav-pending-count")).ToHaveTextAsync("1");

        await item.GetByRole(AriaRole.Button, new() { Name = "Resolve" }).ClickAsync();
        var dialog = MatchE2E.MatchDialog(page);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AdminResolutionStatus })).ToBeVisibleAsync();
        var panel = MatchE2E.Section(dialog, MatchE2E.AdminPanel);
        await MatchE2E.PickAdminScoreAsync(panel, 2, 1);
        await MatchE2E.ClickAsync(panel.GetByRole(AriaRole.Button, new() { Name = "Resolve result" }));
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();

        // The resolved match leaves the queue once the dialog closes.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close match details" }).ClickAsync();
        await Expect(item).ToHaveCountAsync(0);
        await Expect(page.Locator("#match-resolutions-empty")).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task QueueIsEmptyWhenNothingNeedsAnAdminDecision()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("queue-empty-admin"), isAdmin: true);

        var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, "admin/match-resolutions"));
        await page.WaitForInteractiveAsync();

        await Expect(page.Locator("#match-resolutions-empty")).ToBeVisibleAsync();
        await Expect(page.Locator("#admin-nav-pending-count")).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task OnlyAdminsCanReachTheQueue()
    {
        var players = await MatchE2E.CreatePersonasAsync(app, "queue-member", 1);

        using var memberApi = app.CreateApiClient(players[0]);
        using var response = await memberApi.GetAsync("v1/lan/matches/pending-resolution");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);

        var context = await app.NewAuthenticatedContextAsync(players[0]);
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, "admin/match-resolutions"));
        await page.WaitForInteractiveAsync();

        await Expect(page.Locator("li.match-resolution-item")).ToHaveCountAsync(0);
        await Expect(page.Locator("#match-resolutions-empty")).ToHaveCountAsync(0);
        await Expect(page.Locator("#admin-nav-pending-count")).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    // Bounded wait: the backend deadline processor runs on a 30 second poll.
    private static async Task WaitForPendingResolutionAsync(HttpClient adminApi, Guid matchId)
    {
        using var timeout = new CancellationTokenSource(ProcessorTimeout);
        while(true)
        {
            var json = await TournamentE2E.ReadJsonAsync(await adminApi.GetAsync("v1/lan/matches/pending-resolution", timeout.Token));
            if(json.ValueKind == JsonValueKind.Array &&
               json.EnumerateArray().Any(item => item.GetProperty("matchId").GetGuid() == matchId))
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
        }
    }
}
