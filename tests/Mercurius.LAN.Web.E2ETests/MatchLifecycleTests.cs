using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The participant-facing half of the match state machine: who may act, the ended
/// confirmations, score reporting / confirmation / dispute / correction, forfeits and the
/// visibility rules for anonymous and non-participant visitors.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchLifecycleTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task AnonymousVisitorSeesPublicMatchStateAndASignInPromptOnly()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-anon-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-anon-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Both sides must confirm that the match has ended.")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Sign in to confirm your side, report a score, or forfeit. Match results remain publicly viewable.")).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Sign in" })).ToBeVisibleAsync();

        await Expect(MatchE2E.Section(dialog, MatchE2E.YourActionsPanel)).ToHaveCountAsync(0);
        await Expect(MatchE2E.Section(dialog, MatchE2E.AdminPanel)).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task AuthenticatedNonParticipantGetsNoMatchActions()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-outsider-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-outsider-player", 2);
        var outsider = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-outsider"));
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewAuthenticatedContextAsync(outsider);
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();
        await Expect(MatchE2E.Section(dialog, MatchE2E.YourActionsPanel)).ToHaveCountAsync(0);
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.ConfirmEndedButton })).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task ConfirmingTheEndIsRecordedForThatSideOnly()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-confirm-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-confirm-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewAuthenticatedContextAsync(players[0]);
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);
        await MatchE2E.ConfirmEndedAsync(dialog);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Confirmed", new() { Exact = true })).ToHaveCountAsync(1);
        await Expect(dialog.GetByText("Waiting", new() { Exact = true })).ToHaveCountAsync(1);
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.ConfirmEndedButton })).ToHaveCountAsync(0);

        await MatchE2E.CloseDialogAsync(dialog);
        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();

        var reopened = await MatchE2E.OpenMatchAsync(page, players[0].Username);
        await Expect(reopened.GetByText("Confirmed", new() { Exact = true })).ToHaveCountAsync(1);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BothSidesConfirmingTheEndUnlocksScoreReportingInSeparateSessions()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-both-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-both-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var firstContext = await app.NewAuthenticatedContextAsync(players[0]);
        var secondContext = await app.NewAuthenticatedContextAsync(players[1]);
        var firstPage = await OpenTournamentAsync(firstContext, tournamentId);
        var secondPage = await OpenTournamentAsync(secondContext, tournamentId);

        var firstDialog = await MatchE2E.OpenMatchAsync(firstPage, players[0].Username);
        await MatchE2E.ConfirmEndedAsync(firstDialog);
        await Expect(firstDialog.GetByText("Waiting", new() { Exact = true })).ToHaveCountAsync(1);

        var secondDialog = await MatchE2E.OpenMatchAsync(secondPage, players[1].Username);
        await MatchE2E.ConfirmEndedAsync(secondDialog);

        await Expect(secondDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ReadyForScore })).ToBeVisibleAsync();
        await Expect(secondDialog.GetByText("Both sides have confirmed the end. Either eligible participant or captain may submit the score."))
            .ToBeVisibleAsync();
        await Expect(secondDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToBeVisibleAsync();

        await firstPage.ReloadAsync();
        await firstPage.WaitForInteractiveAsync();
        var refreshedDialog = await MatchE2E.OpenMatchAsync(firstPage, players[0].Username);
        await Expect(refreshedDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ReadyForScore })).ToBeVisibleAsync();
        await Expect(refreshedDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, firstContext, secondContext);
    }

    [Fact]
    public async Task FirstScoreReportOpensTheConfirmationWindowAndLocksTheReporter()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-report-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-report-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);

        var firstContext = await app.NewAuthenticatedContextAsync(players[0]);
        var firstPage = await OpenTournamentAsync(firstContext, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(firstPage, players[0].Username);
        await MatchE2E.SubmitScoreAsync(dialog, 2, 1);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ScoreConfirmationStatus })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("The first score report is saved. The opponent has five minutes to agree or report a correction."))
            .ToBeVisibleAsync();
        await Expect(MatchE2E.Section(dialog, MatchE2E.DeadlineLabel)).ToBeVisibleAsync();
        await Expect(MatchE2E.Section(dialog, MatchE2E.DeadlineLabel)).ToContainTextAsync("Window closes");
        await Expect(dialog.GetByText("Your report:")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("2-1")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Opponent report:")).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToHaveCountAsync(0);

        var secondContext = await app.NewAuthenticatedContextAsync(players[1]);
        var secondPage = await OpenTournamentAsync(secondContext, tournamentId);
        var opponentDialog = await MatchE2E.OpenMatchAsync(secondPage, players[1].Username);

        await Expect(opponentDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ScoreConfirmationStatus })).ToBeVisibleAsync();
        await Expect(opponentDialog.GetByText("Opponent report:")).ToBeVisibleAsync();
        await Expect(opponentDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, firstContext, secondContext);
    }

    [Fact]
    public async Task OpponentAgreeingWithTheScoreCompletesTheMatch()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-agree-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-agree-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        await MatchE2E.SubmitScoreViaApiAsync(app, (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id, players[0], 2, 1);

        var secondContext = await app.NewAuthenticatedContextAsync(players[1]);
        var page = await OpenTournamentAsync(secondContext, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[1].Username);
        await MatchE2E.SubmitScoreAsync(dialog, 2, 1);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("The result is official and has advanced the bracket.")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Winner", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Loser", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("[2]");
        await Expect(dialog).ToContainTextAsync("[1]");

        await MatchE2E.CloseContextsAsync(app, secondContext);
    }

    [Fact]
    public async Task OpponentReportingADifferentScoreOpensTheCorrectionWindow()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-dispute-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-dispute-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        await MatchE2E.SubmitScoreViaApiAsync(app, (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id, players[0], 2, 1);

        var secondContext = await app.NewAuthenticatedContextAsync(players[1]);
        var page = await OpenTournamentAsync(secondContext, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[1].Username);
        await MatchE2E.SubmitScoreAsync(dialog, 2, 0);

        // Different reports open the correction window immediately; the reporter can correct once before it expires.
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.DisputedStatus })).ToBeVisibleAsync();
        await Expect(MatchE2E.Section(dialog, MatchE2E.DeadlineLabel)).ToContainTextAsync("Window closes");
        await Expect(dialog.GetByText("Opponent report:")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("2-0")).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, secondContext);
    }

    [Fact]
    public async Task DisputedMatchAllowsExactlyOneCorrectionPerSideAndCompletesWhenTheyAgree()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-correct-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-correct-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[0], 2, 1);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[1], 2, 0);
        // The disputed state is only reached when the confirmation window is evaluated, so the
        // correction window is opened first through the isolated database timestamp.
        await MatchE2E.ExpireScoreWindowAsync(app, matchId);

        var firstContext = await app.NewAuthenticatedContextAsync(players[0]);
        var firstPage = await OpenTournamentAsync(firstContext, tournamentId);
        var firstDialog = await MatchE2E.OpenMatchAsync(firstPage, players[0].Username);
        await Expect(firstDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.DisputedStatus })).ToBeVisibleAsync();

        await MatchE2E.SubmitScoreAsync(firstDialog, 0, 2);
        await Expect(firstDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.DisputedStatus })).ToBeVisibleAsync();
        await Expect(firstDialog.GetByText("Your report:")).ToBeVisibleAsync();
        await Expect(firstDialog.GetByText("0-2")).ToBeVisibleAsync();
        await Expect(firstDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToHaveCountAsync(0);

        var secondContext = await app.NewAuthenticatedContextAsync(players[1]);
        var secondPage = await OpenTournamentAsync(secondContext, tournamentId);
        var secondDialog = await MatchE2E.OpenMatchAsync(secondPage, players[1].Username);
        await Expect(secondDialog.GetByText("0-2")).ToBeVisibleAsync();
        await Expect(secondDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToBeVisibleAsync();

        await MatchE2E.SubmitScoreAsync(secondDialog, 0, 2);
        await Expect(secondDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(secondDialog.GetByText("Winner", new() { Exact = true })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, firstContext, secondContext);
    }

    [Fact]
    public async Task TeamCaptainSeesCaptainActionsAndCanConfirmTheEnd()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-team-admin"), isAdmin: true);
        var captains = await MatchE2E.CreatePersonasAsync(app, "match-team-captain", 2);
        var teams = new List<(E2EPersona Captain, string TeamName)>
        {
            (captains[0], $"E2E Match Team {Guid.NewGuid().ToString("N")[..8]}"),
            (captains[1], $"E2E Match Team {Guid.NewGuid().ToString("N")[..8]}")
        };
        var tournamentId = await MatchE2E.StartTeamTournamentAsync(app, app.CreateApiClient(admin), teams);

        var context = await app.NewAuthenticatedContextAsync(captains[0]);
        var page = await OpenTournamentAsync(context, tournamentId);
        await Expect(page.Locator("#tournament-bracket").GetByText(teams[0].TeamName, new() { Exact = true })).ToBeVisibleAsync();

        var dialog = await MatchE2E.OpenMatchAsync(page, teams[0].TeamName);
        var panel = MatchE2E.Section(dialog, MatchE2E.YourActionsPanel);
        await Expect(panel).ToBeVisibleAsync();
        await Expect(panel.GetByText("Your captain actions")).ToBeVisibleAsync();

        await MatchE2E.ConfirmEndedAsync(dialog);
        await Expect(dialog.GetByText("Confirmed", new() { Exact = true })).ToHaveCountAsync(1);
        await Expect(dialog.GetByText("Waiting", new() { Exact = true })).ToHaveCountAsync(1);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task ParticipantCanCancelAndThenConfirmAForfeit()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-forfeit-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-forfeit-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewAuthenticatedContextAsync(players[0]);
        var page = await OpenTournamentAsync(context, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);

        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.ForfeitSideButton }));
        var confirmation = dialog.GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm match forfeit" });
        await Expect(confirmation).ToBeVisibleAsync();
        await MatchE2E.ClickAsync(confirmation.GetByRole(AriaRole.Button, new() { Name = "Keep playing" }));
        await Expect(confirmation).ToHaveCountAsync(0);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();

        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.ForfeitSideButton }));
        await MatchE2E.ClickAsync(dialog
            .GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm match forfeit" })
            .GetByRole(AriaRole.Button, new() { Name = "Confirm forfeit" }));

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ForfeitedStatus })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("The result is official after a side forfeited.")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Winner", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Loser", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.ForfeitSideButton })).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task CompletedResultIsPersistedIntoTheBracketAfterReload()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-persist-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-persist-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[0], 2, 1);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[1], 2, 1);

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var card = MatchE2E.MatchCards(page, players[0].Username).First;
        await Expect(card).ToContainTextAsync("[2]");
        await Expect(card).ToContainTextAsync("[1]");

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();

        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Winner", new() { Exact = true })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    private async Task<IPage> OpenTournamentAsync(IBrowserContext context, Guid tournamentId)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();
        return page;
    }
}
