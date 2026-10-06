using System.Net;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The administrator half of match management: the gated admin panel, resolution of a
/// disputed match, reversal with its confirmation and cancellation paths, forced forfeits,
/// tournament-lifecycle locking, and the authorization boundaries of the mutation endpoints.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchAdminActionTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task AdminPanelOffersForfeitAndExplainsWhyReversalIsUnavailableOnALiveMatch()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("admin-panel-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "admin-panel-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await OpenTournamentAsync(context, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);

        var panel = MatchE2E.Section(dialog, MatchE2E.AdminPanel);
        await Expect(panel).ToBeVisibleAsync();
        // A live, undisputed match renders the reversal block (the resolve block only appears once
        // the match is disputed), while recording a forfeit stays available.
        await Expect(panel.GetByText(MatchE2E.NoOfficialResultToReverse)).ToBeVisibleAsync();
        await Expect(panel.GetByText("Record a forfeit")).ToBeVisibleAsync();
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = $"Forfeit {players[0].Username}" })).ToBeVisibleAsync();
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = $"Forfeit {players[1].Username}" })).ToBeVisibleAsync();
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Reverse official result" })).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task ParticipantDoesNotSeeTheAdministratorControls()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("admin-hidden-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "admin-hidden-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewAuthenticatedContextAsync(players[0]);
        var page = await OpenTournamentAsync(context, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);

        await Expect(MatchE2E.Section(dialog, MatchE2E.YourActionsPanel)).ToBeVisibleAsync();
        await Expect(MatchE2E.Section(dialog, MatchE2E.AdminPanel)).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task AdminResolvesADisputedMatchWithTheVerifiedScore()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("admin-resolve-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "admin-resolve-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: "BestOf3");
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;
        await MatchE2E.ReachReadyForScoreAsync(app, tournamentId, players[0], players[1]);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[0], 2, 1);
        await MatchE2E.SubmitScoreViaApiAsync(app, matchId, players[1], 2, 0);

        var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await OpenTournamentAsync(context, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.DisputedStatus })).ToBeVisibleAsync();
        var panel = MatchE2E.Section(dialog, MatchE2E.AdminPanel);
        await Expect(panel.GetByText("Resolve the conflict with the verified final score. The selected result becomes official."))
            .ToBeVisibleAsync();
        await Expect(panel.GetByText("Enter both participant scores before resolving this match.")).ToBeVisibleAsync();
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Resolve result" })).ToBeDisabledAsync();

        await MatchE2E.FillAdminScoreAsync(panel, players[0].Username, 2);
        await MatchE2E.FillAdminScoreAsync(panel, players[1].Username, 0);
        // The hint is rendered by the server from the bound values, so its removal proves the
        // circuit received both scores; the button must then be clickable.
        await Expect(panel.GetByText(MatchE2E.EnterScoresBeforeResolve)).ToHaveCountAsync(0);
        var resolve = panel.GetByRole(AriaRole.Button, new() { Name = "Resolve result" });
        await Expect(resolve).ToBeEnabledAsync();
        await MatchE2E.ClickAsync(resolve);

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("[2]");
        await Expect(dialog).ToContainTextAsync("[0]");
        await Expect(page.GetByText("The match was resolved and the result is official.")).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task AdminReversalConfirmationCanBeCancelledWithoutChangingTheResult()
    {
        var setup = await StartCompletedMatchAsync("admin-reverse-cancel");

        var context = await app.NewAuthenticatedContextAsync(setup.Admin);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.Players[0].Username);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();

        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Reverse official result" }));
        var confirmation = dialog.GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm reversal" });
        await Expect(confirmation).ToBeVisibleAsync();
        await MatchE2E.ClickAsync(confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel" }));

        await Expect(confirmation).ToHaveCountAsync(0);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("[1]");
        await Expect(dialog).ToContainTextAsync("[0]");

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        var reopened = await MatchE2E.OpenMatchAsync(page, setup.Players[0].Username);
        await Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task AdminReversalClearsTheOfficialResult()
    {
        var setup = await StartCompletedMatchAsync("admin-reverse");

        var context = await app.NewAuthenticatedContextAsync(setup.Admin);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.Players[0].Username);

        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Reverse official result" }));
        await MatchE2E.ClickAsync(dialog
            .GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm reversal" })
            .GetByRole(AriaRole.Button, new() { Name = "Confirm reversal" }));

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ReversedStatus })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("The result was reversed. The match can be played again when both sides are assigned."))
            .ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("[?]");
        await Expect(dialog.GetByText("Winner", new() { Exact = true })).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task AdminForfeitConfirmationCanBeCancelledThenRecordsTheResult()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("admin-forfeit-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "admin-forfeit-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await OpenTournamentAsync(context, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);

        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = $"Forfeit {players[1].Username}" }));
        var confirmation = dialog.GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm administrator forfeit" });
        await Expect(confirmation).ToBeVisibleAsync();
        await Expect(confirmation).ToContainTextAsync($"Forfeit {players[1].Username}?");
        await MatchE2E.ClickAsync(confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel" }));

        await Expect(confirmation).ToHaveCountAsync(0);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();

        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = $"Forfeit {players[1].Username}" }));
        await MatchE2E.ClickAsync(dialog
            .GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm administrator forfeit" })
            .GetByRole(AriaRole.Button, new() { Name = "Confirm forfeit" }));

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ForfeitedStatus })).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("[1]");
        await Expect(dialog).ToContainTextAsync("[0]");
        await Expect(dialog.GetByText("Winner", new() { Exact = true })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task MatchActionsAreLockedOnceTheTournamentLeavesInProgress()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("admin-locked-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "admin-locked-player", 2);
        var adminApi = app.CreateApiClient(admin);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, adminApi, players);
        await TournamentE2E.SetLifecycleAsync(adminApi, tournamentId, "Canceled");

        var adminContext = await app.NewAuthenticatedContextAsync(admin);
        var adminPage = await OpenTournamentAsync(adminContext, tournamentId);
        var adminDialog = await MatchE2E.OpenMatchAsync(adminPage, players[0].Username);

        await Expect(adminDialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();
        // Both the resolve and the force-forfeit control explain their own block, so every
        // blocked-reason paragraph in the panel must carry the same tournament-level reason.
        var blockedReasons = await MatchE2E.Section(adminDialog, MatchE2E.AdminPanel)
            .GetByRole(AriaRole.Paragraph)
            .AllTextContentsAsync();
        Assert.NotEmpty(blockedReasons);
        Assert.All(blockedReasons, reason => Assert.Equal(MatchE2E.TournamentNotInProgress, reason.Trim()));

        var playerContext = await app.NewAuthenticatedContextAsync(players[0]);
        var playerPage = await OpenTournamentAsync(playerContext, tournamentId);
        var playerDialog = await MatchE2E.OpenMatchAsync(playerPage, players[0].Username);

        await Expect(MatchE2E.Section(playerDialog, MatchE2E.YourActionsPanel)).ToBeVisibleAsync();
        await Expect(playerDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.ConfirmEndedButton })).ToHaveCountAsync(0);
        await Expect(playerDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToHaveCountAsync(0);
        await Expect(playerDialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.ForfeitSideButton })).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, adminContext, playerContext);
    }

    [Fact]
    public async Task MatchMutationEndpointsRejectAnonymousNonParticipantAndNonAdminCallers()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("admin-api-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "admin-api-player", 2);
        var outsider = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("admin-api-outsider"));
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);
        var matchId = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single().Id;

        var anonymousConfirm = await app.Api.PostAsync($"v1/lan/matches/{matchId}/confirm-ended", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousConfirm.StatusCode);

        var anonymousScore = await TournamentE2E.SendJsonAsync(
            app.Api, HttpMethod.Put, $"v1/lan/matches/{matchId}/score", new { Participant1Score = 1, Participant2Score = 0 });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousScore.StatusCode);

        var anonymousState = await app.Api.GetAsync($"v1/lan/matches/{matchId}/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousState.StatusCode);

        var outsiderConfirm = await app.CreateApiClient(outsider).PostAsync($"v1/lan/matches/{matchId}/confirm-ended", null);
        Assert.True(
            outsiderConfirm.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected the participant guard to reject an outsider, got {(int)outsiderConfirm.StatusCode}.");

        var participantResolve = await app.CreateApiClient(players[0]).PostAsync($"v1/lan/matches/{matchId}/resolve", null);
        Assert.Equal(HttpStatusCode.Forbidden, participantResolve.StatusCode);
    }

    private async Task<(Guid TournamentId, E2EPersona Admin, IReadOnlyList<E2EPersona> Players)> StartCompletedMatchAsync(string prefix)
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel($"{prefix}-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, $"{prefix}-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);
        var match = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single();
        var participant1 = MatchE2E.PersonaForUser(players, match.UserParticipant1Id);
        var participant2 = MatchE2E.PersonaForUser(players, match.UserParticipant2Id);
        await MatchE2E.CompleteMatchViaApiAsync(app, match.Id, participant1, participant2, 1, 0);
        return (tournamentId, admin, players);
    }

    private async Task<IPage> OpenTournamentAsync(IBrowserContext context, Guid tournamentId)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();
        return page;
    }
}
