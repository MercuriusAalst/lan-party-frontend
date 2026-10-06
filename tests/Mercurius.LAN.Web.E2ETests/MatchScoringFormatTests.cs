using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The best-of formats drive both the badge shown in the dialog and the decisive-score
/// rules the backend enforces; these tests cover the accepted and rejected shapes for
/// Bo1, Bo3 and Bo5 through the real UI.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchScoringFormatTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Theory]
    [InlineData("BestOf1", "Best of 1")]
    [InlineData("BestOf3", "Best of 3")]
    [InlineData("BestOf5", "Best of 5")]
    public async Task DialogShowsTheConfiguredMatchFormat(string format, string expectedLabel)
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-format-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-format-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: format);

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);
        await Expect(dialog.GetByText(expectedLabel, new() { Exact = true })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BestOfOneRejectsADrawReportAndKeepsTheMatchUnresolved()
    {
        var setup = await StartReadyToScoreAsync("BestOf1");

        var context = await app.NewAuthenticatedContextAsync(setup.First);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        await MatchE2E.SubmitScoreAsync(dialog, 1, 1);

        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync(MatchE2E.ActionSaveFailed);
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToHaveCountAsync(0);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        var reopened = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        await Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ReadyForScore })).ToBeVisibleAsync();
        await Expect(reopened.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BestOfThreeRejectsAnIncompleteReport()
    {
        var setup = await StartReadyToScoreAsync("BestOf3");

        var context = await app.NewAuthenticatedContextAsync(setup.First);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        await MatchE2E.SubmitScoreAsync(dialog, 1, 0);

        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync(MatchE2E.ActionSaveFailed);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        var reopened = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        await Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ReadyForScore })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BestOfThreeRejectsAScoreBeyondTheDecisiveWin()
    {
        var setup = await StartReadyToScoreAsync("BestOf3");

        var context = await app.NewAuthenticatedContextAsync(setup.First);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        await MatchE2E.SubmitScoreAsync(dialog, 3, 0);

        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync(MatchE2E.ActionSaveFailed);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        var reopened = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        await Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ReadyForScore })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BestOfFiveCompletesWhenBothSidesReportThreeToOne()
    {
        var setup = await StartReadyToScoreAsync("BestOf5");
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, setup.TournamentId)).Single().Id;
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, setup.First, 3, 1);

        var context = await app.NewAuthenticatedContextAsync(setup.Second);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.Second.Username);
        await MatchE2E.SubmitScoreAsync(dialog, 3, 1);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("[3]");
        await Expect(dialog).ToContainTextAsync("[1]");

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BestOfThreeRejectsAScoreThatIsLevelAtTheDecisiveWin()
    {
        var setup = await StartReadyToScoreAsync("BestOf3");

        var context = await app.NewAuthenticatedContextAsync(setup.Second);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.Second.Username);
        await MatchE2E.SubmitScoreAsync(dialog, 2, 2);

        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync(MatchE2E.ActionSaveFailed);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    private async Task<(Guid TournamentId, E2EPersona First, E2EPersona Second)> StartReadyToScoreAsync(string format)
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-score-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-score-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: format);
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        return (tournamentId, players[0], players[1]);
    }

    private async Task<IPage> OpenTournamentAsync(IBrowserContext context, Guid tournamentId)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();
        return page;
    }
}
