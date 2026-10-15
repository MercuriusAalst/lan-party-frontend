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
    public async Task BestOfOneNeedsOnlyTheWinnerAndShowsNoLoserStep()
    {
        var setup = await StartReadyToScoreAsync("BestOf1");

        var context = await app.NewAuthenticatedContextAsync(setup.First);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        var submit = dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton });

        await Expect(dialog.GetByRole(AriaRole.Group, new() { Name = "Who won?" })).ToBeVisibleAsync();
        await Expect(submit).ToBeDisabledAsync();

        await MatchE2E.ClickAsync(dialog.Locator("#player-winner-1"));
        await Expect(dialog.Locator("#player-winner-1")).ToBeCheckedAsync();
        await Expect(submit).ToBeEnabledAsync();
        await Expect(dialog.Locator("input[name='player-loser']")).ToHaveCountAsync(0);
        await Expect(dialog.Locator("#player-score-summary")).ToContainTextAsync("1 – 0");

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        var reopened = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        await Expect(reopened.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingScore })).ToBeVisibleAsync();
        await Expect(reopened.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton })).ToBeDisabledAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Theory]
    [InlineData("BestOf3", new[] { 0, 1 })]
    [InlineData("BestOf5", new[] { 0, 1, 2 })]
    public async Task LoserStepOffersExactlyTheScoresBelowTheRequiredWins(string format, int[] expectedLoserScores)
    {
        var setup = await StartReadyToScoreAsync(format);

        var context = await app.NewAuthenticatedContextAsync(setup.First);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.First.Username);
        var submit = dialog.GetByRole(AriaRole.Button, new() { Name = MatchE2E.SubmitScoreButton });
        var loserOptions = dialog.Locator("input[name='player-loser']");

        // Nothing is selected and the loser step stays hidden until a winner is chosen.
        await Expect(submit).ToBeDisabledAsync();
        await Expect(loserOptions).ToHaveCountAsync(0);

        await MatchE2E.ClickAsync(dialog.Locator("#player-winner-2"));
        await Expect(loserOptions).ToHaveCountAsync(expectedLoserScores.Length);
        for(var index = 0; index < expectedLoserScores.Length; index++)
            await Expect(loserOptions.Nth(index)).ToHaveAttributeAsync("value", expectedLoserScores[index].ToString());
        await Expect(submit).ToBeDisabledAsync();

        var loserScore = expectedLoserScores[^1];
        await MatchE2E.ClickAsync(dialog.Locator($"#player-loser-{loserScore}"));
        await Expect(submit).ToBeEnabledAsync();
        await Expect(dialog.Locator("#player-score-summary")).ToContainTextAsync($"{loserScore} – {expectedLoserScores.Length}");

        // Switching the winner clears the loser choice, so the selection must be completed again.
        await MatchE2E.ClickAsync(dialog.Locator("#player-winner-1"));
        await Expect(loserOptions.First).Not.ToBeCheckedAsync();
        await Expect(submit).ToBeDisabledAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task FeedbackToastAppearsAboveTheOpenDialog()
    {
        var setup = await StartReadyToScoreAsync("BestOf3");

        var context = await app.NewAuthenticatedContextAsync(setup.First);
        var page = await OpenTournamentAsync(context, setup.TournamentId);
        var dialog = await MatchE2E.OpenMatchAsync(page, setup.First.Username);

        await MatchE2E.SubmitScoreAsync(dialog, 2, 1);

        // The dialog stays open while the confirmation window runs; the toast must be what the user sees on top of it.
        await Expect(dialog).ToBeVisibleAsync();
        var toast = page.Locator(".blazored-toast-message").Last;
        await Expect(toast).ToBeVisibleAsync();
        var toastIsOnTop = await toast.EvaluateAsync<bool>(
            "element => { const box = element.getBoundingClientRect(); const hit = document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2); return hit !== null && element.contains(hit); }");
        Assert.True(toastIsOnTop, "Expected the toast to be the topmost element at its own centre, above the match dialog overlay.");

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

    private async Task<(Guid TournamentId, E2EPersona First, E2EPersona Second)> StartReadyToScoreAsync(string format)
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("match-score-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "match-score-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, format: format);
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
