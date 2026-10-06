using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The two time-bound transitions of the match state machine. The score confirmation and
/// correction windows are real five-minute clocks, so the deadline column is moved into the
/// past through the isolated database and the resulting state is read back through the UI.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchDeadlineTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task OneSidedReportBecomesOfficialWhenTheConfirmationWindowExpires()
    {
        var setup = await StartScoredSingleMatchAsync("deadline-score", report1: 2, report2: 1);
        await MatchE2E.ExpireScoreWindowAsync(app, setup.MatchId);

        var context = await app.NewContextAsync();
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.First.Username);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("The result is official and has advanced the bracket.")).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("[2]");
        await Expect(dialog).ToContainTextAsync("[1]");
        await Expect(MatchE2E.Section(dialog, MatchE2E.DeadlineLabel)).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task DisputeEscalatesToAdminResolutionWhenTheCorrectionWindowExpires()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("deadline-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "deadline-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[0], 2, 1);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[1], 2, 0);
        await MatchE2E.ExpireScoreWindowAsync(app, matchId, correction: true);

        var playerContext = await app.NewAuthenticatedContextAsync(players[0]);
        var playerPage = await OpenTournamentAsync(playerContext, tournamentId);
        var playerDialog = await MatchE2E.OpenMatchAsync(playerPage, players[0].Username);

        await Expect(playerDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AdminResolutionStatus })).ToBeVisibleAsync();
        await Expect(playerDialog.GetByText("The correction window expired. An authorized tournament administrator must resolve this result."))
            .ToBeVisibleAsync();
        await Expect(playerDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToHaveCountAsync(0);

        var adminContext = await app.NewAuthenticatedContextAsync(admin);
        var adminPage = await OpenTournamentAsync(adminContext, tournamentId);
        var adminDialog = await MatchE2E.OpenMatchAsync(adminPage, players[0].Username);

        var panel = MatchE2E.Section(adminDialog, MatchE2E.AdminPanel);
        await Expect(adminDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AdminResolutionStatus })).ToBeVisibleAsync();
        await MatchE2E.FillAdminScoreAsync(panel, players[0].Username, 2);
        await MatchE2E.FillAdminScoreAsync(panel, players[1].Username, 0);
        await Expect(panel.GetByText(MatchE2E.EnterScoresBeforeResolve)).ToHaveCountAsync(0);
        var resolve = panel.GetByRole(AriaRole.Button, new() { Name = "Resolve result" });
        await Expect(resolve).ToBeEnabledAsync();
        await MatchE2E.ClickAsync(resolve);

        await Expect(adminDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(adminDialog).ToContainTextAsync("[2]");
        await Expect(adminDialog).ToContainTextAsync("[0]");

        await MatchE2E.CloseContextsAsync(app, playerContext, adminContext);
    }

    [Fact]
    public async Task UnconfirmedMatchIsUnaffectedByAnUnrelatedPastDeadline()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("deadline-noop-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "deadline-noop-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;

        await MatchE2E.ExpireScoreWindowAsync(app, matchId);

        var context = await app.NewContextAsync();
        var page = await OpenTournamentAsync(context, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Both sides must confirm that the match has ended.")).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    private async Task<(Guid TournamentId, Guid MatchId, E2EPersona First, E2EPersona Second)> StartScoredSingleMatchAsync(
        string prefix, int report1, int report2)
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel($"{prefix}-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, $"{prefix}-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[0], report1, report2);
        return (tournamentId, matchId, players[0], players[1]);
    }

    private async Task<IPage> OpenTournamentAsync(IBrowserContext context, Guid tournamentId)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();
        return page;
    }
}
