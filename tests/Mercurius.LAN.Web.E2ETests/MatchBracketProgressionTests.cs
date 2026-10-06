using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// How a decided match changes the rest of the bracket: the winner filling the next slot,
/// reversal clearing that slot again, the guard that stops a reversal once the linked match
/// has been played, and the placements published when the tournament is finished.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchBracketProgressionTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task CompletingASemiFinalAdvancesItsWinnerIntoTheFinalCard()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("progress-advance-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "progress-advance-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var semiFinal = await FirstRoundMatchAsync(tournamentId);
        var first = MatchE2E.PersonaForUser(players, semiFinal.UserParticipant1Id);
        var second = MatchE2E.PersonaForUser(players, semiFinal.UserParticipant2Id);

        var firstContext = await app.NewAuthenticatedContextAsync(first);
        var secondContext = await app.NewAuthenticatedContextAsync(second);

        await InMatchAsync(firstContext, tournamentId, first.Username, dialog => MatchE2E.ConfirmEndedAsync(dialog));
        await InMatchAsync(secondContext, tournamentId, second.Username, dialog => MatchE2E.ConfirmEndedAsync(dialog));
        await InMatchAsync(firstContext, tournamentId, first.Username,
            dialog => MatchE2E.SubmitScoreAsync(dialog, 1, 0));
        await InMatchAsync(secondContext, tournamentId, second.Username,
            dialog => MatchE2E.SubmitScoreAsync(dialog, 1, 0));

        var context = await app.NewContextAsync();
        var page = await OpenTournamentAsync(context, tournamentId);

        // The winner now appears in their own semi-final and in the final slot it feeds.
        await Expect(MatchE2E.MatchCards(page, first.Username)).ToHaveCountAsync(2);
        await Expect(page.Locator("#tournament-bracket").GetByText("TBD")).ToHaveCountAsync(1);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        await Expect(MatchE2E.MatchCards(page, first.Username)).ToHaveCountAsync(2);

        var dialog = await MatchE2E.OpenMatchAsync(page, first.Username);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, firstContext, secondContext, context);
    }

    [Fact]
    public async Task AdminReversalClearsTheAdvancedDownstreamSlot()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("progress-reverse-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "progress-reverse-player", 4);
        var adminApi = app.CreateApiClient(admin);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, adminApi, players);

        var semiFinal = await FirstRoundMatchAsync(tournamentId);
        var first = MatchE2E.PersonaForUser(players, semiFinal.UserParticipant1Id);
        var second = MatchE2E.PersonaForUser(players, semiFinal.UserParticipant2Id);
        await MatchE2E.CompleteMatchViaApiAsync(app, semiFinal.Id, first, second, 1, 0);

        var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await OpenTournamentAsync(context, tournamentId);
        await Expect(MatchE2E.MatchCards(page, first.Username)).ToHaveCountAsync(2);

        var dialog = await MatchE2E.OpenMatchAsync(page, first.Username);
        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Reverse official result" }));
        await MatchE2E.ClickAsync(dialog
            .GetByRole(AriaRole.Alertdialog, new() { Name = "Confirm reversal" })
            .GetByRole(AriaRole.Button, new() { Name = "Confirm reversal" }));

        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.ReversedStatus })).ToBeVisibleAsync();
        await MatchE2E.CloseDialogAsync(dialog);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        await Expect(MatchE2E.MatchCards(page, first.Username)).ToHaveCountAsync(1);
        await Expect(page.Locator("#tournament-bracket").GetByText("TBD")).ToHaveCountAsync(2);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task ReversalIsBlockedOnceTheLinkedFinalHasAResult()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("progress-blocked-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "progress-blocked-player", 4);
        var adminApi = app.CreateApiClient(admin);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, adminApi, players);

        var roundOne = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId))
            .Where(match => match.RoundNumber == 1)
            .OrderBy(match => match.MatchNumber)
            .ToList();

        foreach (var match in roundOne)
        {
            var first = MatchE2E.PersonaForUser(players, match.UserParticipant1Id);
            var second = MatchE2E.PersonaForUser(players, match.UserParticipant2Id);
            await MatchE2E.CompleteMatchViaApiAsync(app, match.Id, first, second, 1, 0);
        }

        var final = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single(match => match.RoundNumber > 1);
        await MatchE2E.CompleteMatchViaApiAsync(
            app,
            final.Id,
            MatchE2E.PersonaForUser(players, final.UserParticipant1Id),
            MatchE2E.PersonaForUser(players, final.UserParticipant2Id),
            1,
            0);

        var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await OpenTournamentAsync(context, tournamentId);
        // The losing side only appears in this match, so the card is unambiguous.
        var eliminated = MatchE2E.PersonaForUser(players, roundOne[0].UserParticipant2Id);
        var dialog = await MatchE2E.OpenMatchAsync(page, eliminated.Username);

        var panel = MatchE2E.Section(dialog, MatchE2E.AdminPanel);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();
        await Expect(panel.GetByText("This result cannot be reversed because a linked downstream match has already been played or resolved."))
            .ToBeVisibleAsync();
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Reverse official result" })).ToHaveCountAsync(0);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        var reopened = await MatchE2E.OpenMatchAsync(page, eliminated.Username);
        await Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.CompletedStatus })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task WinningTheFinalThenFinishingTheTournamentPublishesPlacements()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("progress-places-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "progress-places-player", 2);
        var adminApi = app.CreateApiClient(admin);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, adminApi, players);

        var final = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single();
        var winner = MatchE2E.PersonaForUser(players, final.UserParticipant1Id);
        var runnerUp = MatchE2E.PersonaForUser(players, final.UserParticipant2Id);

        // The winning report is confirmed by the API first so the Finish step starts from a
        // decided final; this keeps the assertion below focused on the published placements.
        await MatchE2E.ConfirmEndedViaApiAsync(app, final.Id, winner);
        await MatchE2E.ConfirmEndedViaApiAsync(app, final.Id, runnerUp);
        await MatchE2E.SubmitScoreViaApiAsync(app, final.Id, winner, 1, 0);
        await MatchE2E.SubmitScoreViaApiAsync(app, final.Id, runnerUp, 1, 0);

        var winnerContext = await app.NewAuthenticatedContextAsync(winner);
        var runnerUpContext = await app.NewAuthenticatedContextAsync(runnerUp);

        var winnerPage = await OpenTournamentAsync(winnerContext, tournamentId);
        await Expect(winnerPage.Locator("#tournament-results").GetByText("No results found.")).ToBeVisibleAsync();

        var adminContext = await app.NewAuthenticatedContextAsync(admin);
        var adminPage = await OpenTournamentAsync(adminContext, tournamentId);
        var adminActions = adminPage.Locator(".Tournament-admin-actions");
        await MatchE2E.ClickAsync(adminActions.GetByRole(AriaRole.Button, new() { Name = "Finish" }));
        await Expect(adminPage.Locator(".Tournament-status").First).ToHaveTextAsync("Finished");

        var results = adminPage.Locator("#tournament-results");
        await Expect(results.GetByText(winner.Username, new() { Exact = true })).ToBeVisibleAsync();
        await Expect(results.GetByText("1st", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(results.GetByText(runnerUp.Username, new() { Exact = true })).ToBeVisibleAsync();
        await Expect(results.GetByText("2nd", new() { Exact = true })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, winnerContext, runnerUpContext, adminContext);
    }

    [Fact]
    public async Task FinishingATournamentWithAnUnresolvedFinalIsRefusedAndStaysOngoing()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("progress-unresolved-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "progress-unresolved-player", 2);
        var adminApi = app.CreateApiClient(admin);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, adminApi, players);

        // A two-player single-elimination bracket is exactly one match, so its final is still
        // undecided when the tournament is started; the domain refuses to complete that.
        var final = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId)).Single();
        Assert.Equal(1, final.RoundNumber);

        var context = await app.NewAuthenticatedContextAsync(admin);
        try
        {
            var page = await OpenTournamentAsync(context, tournamentId);
            await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync("Ongoing");

            await MatchE2E.ClickAsync(page.Locator(".Tournament-admin-actions")
                .GetByRole(AriaRole.Button, new() { Name = "Finish" }));

            // The refusal comes from the domain (a final without a winner cannot be completed),
            // so the tournament stays ongoing and stays that way across a reload.
            await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync("Ongoing");
            await page.ReloadAsync();
            await page.WaitForInteractiveAsync();
            await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync("Ongoing");
            await Expect(page.Locator("#tournament-results").GetByText("No results found.")).ToBeVisibleAsync();
        }
        finally
        {
            await MatchE2E.CloseContextsAsync(app, context);
        }
    }

    private async Task<MatchE2E.MatchView> FirstRoundMatchAsync(Guid tournamentId)
    {
        var matches = await MatchE2E.GetMatchesAsync(app.Api, tournamentId);
        return matches
            .Where(match => match.RoundNumber == matches.Min(candidate => candidate.RoundNumber))
            .OrderBy(match => match.MatchNumber)
            .First();
    }

    /// <summary>Runs one participant step in a fresh page so every step reads the latest state.</summary>
    private async Task InMatchAsync(
        IBrowserContext context,
        Guid tournamentId,
        string participantUsername,
        Func<ILocator, Task> step)
    {
        var page = await OpenTournamentAsync(context, tournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, participantUsername);
        await step(dialog);
        await page.CloseAsync();
    }

    private async Task<IPage> OpenTournamentAsync(IBrowserContext context, Guid tournamentId)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();
        return page;
    }
}
