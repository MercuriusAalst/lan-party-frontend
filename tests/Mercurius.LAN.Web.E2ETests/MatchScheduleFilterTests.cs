using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The upcoming-matches panel on the tournament detail page: its bracket and round filters,
/// the empty-filtered state, and the round filter that resets when it no longer applies to
/// the selected bracket.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchScheduleFilterTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    // Rendered labels from the schedule panel (en-US).
    private const string AllBrackets = "All brackets";
    private const string MainBracket = "Main bracket";
    private const string LowerBracket = "Lower bracket";
    private const string GrandFinal = "Grand final";
    private const string AllRounds = "All rounds";
    private const string NoFilteredMatches = "No matches match the selected filters.";

    [Fact]
    public async Task SingleEliminationScheduleListsMatchesAndFiltersThemByRound()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("schedule-se-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "schedule-se-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewContextAsync();
        var page = await OpenTournamentAsync(context, tournamentId);
        var schedule = page.Locator("#tournament-schedule");

        var bracketFilter = schedule.Locator("#schedule-bracket-filter");
        var roundFilter = schedule.Locator("#schedule-round-filter");
        await Expect(bracketFilter).ToHaveValueAsync("All");
        await Expect(roundFilter).ToHaveValueAsync(string.Empty);

        var items = schedule.Locator(".Tournament-schedule-item");
        await Expect(items).ToHaveCountAsync(3);

        var rounds = await roundFilter.Locator("option").AllTextContentsAsync();
        Assert.Equal(new[] { AllRounds, "Round 1", "Round 2" }, rounds.Select(option => option.Trim()).ToArray());

        await roundFilter.SelectOptionAsync("1");
        await Expect(items).ToHaveCountAsync(2);

        await roundFilter.SelectOptionAsync("2");
        await Expect(items).ToHaveCountAsync(1);

        await Expect(schedule.Locator(".Tournament-empty-copy")).ToHaveCountAsync(0);
        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BracketFilterWithoutMatchesShowsTheFilteredEmptyState()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("schedule-empty-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "schedule-empty-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, app.CreateApiClient(admin), players);

        var context = await app.NewContextAsync();
        var page = await OpenTournamentAsync(context, tournamentId);
        var schedule = page.Locator("#tournament-schedule");
        var bracketFilter = schedule.Locator("#schedule-bracket-filter");

        await Expect(bracketFilter.Locator("option")).ToHaveTextAsync(new[]
        {
            AllBrackets, MainBracket, LowerBracket, GrandFinal
        });

        await bracketFilter.SelectOptionAsync("Lower");
        await Expect(schedule.GetByText(NoFilteredMatches, new() { Exact = true })).ToBeVisibleAsync();
        await Expect(schedule.Locator(".Tournament-schedule-item")).ToHaveCountAsync(0);

        await bracketFilter.SelectOptionAsync("GrandFinal");
        await Expect(schedule.GetByText(NoFilteredMatches, new() { Exact = true })).ToBeVisibleAsync();

        await bracketFilter.SelectOptionAsync("All");
        await Expect(schedule.GetByText(NoFilteredMatches, new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(schedule.Locator(".Tournament-schedule-item")).ToHaveCountAsync(3);
        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task DoubleEliminationScheduleSplitsBracketsAndResetsUnavailableRoundFilter()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("schedule-de-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "schedule-de-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, "DoubleElimination");

        var context = await app.NewContextAsync();
        var page = await OpenTournamentAsync(context, tournamentId);
        var schedule = page.Locator("#tournament-schedule");
        var bracketFilter = schedule.Locator("#schedule-bracket-filter");
        var roundFilter = schedule.Locator("#schedule-round-filter");
        var items = schedule.Locator(".Tournament-schedule-item");

        // Wait for the panel to resolve (every list item carries an estimated window) before counting.
        await Expect(items.First).ToBeVisibleAsync();
        var allMatches = await items.CountAsync();
        Assert.True(allMatches > 3, $"Expected the double-elimination bracket to schedule more than three matches, found {allMatches}.");

        // The lower bracket only starts once the upper bracket produces losers.
        await bracketFilter.SelectOptionAsync("Main");
        var mainMatches = await items.CountAsync();

        await roundFilter.SelectOptionAsync("1");
        var mainRoundOneMatches = await items.CountAsync();
        Assert.True(mainRoundOneMatches > 0);
        Assert.True(mainRoundOneMatches < mainMatches);

        // Round 1 exists in both the main and lower brackets, so keep the filter.
        await bracketFilter.SelectOptionAsync("Lower");
        await Expect(roundFilter).ToHaveValueAsync("1");
        await Expect(items).ToHaveCountAsync(1);

        // The grand final is round 3, so round 1 must be cleared when switching to it.
        await bracketFilter.SelectOptionAsync("GrandFinal");
        await Expect(roundFilter).ToHaveValueAsync(string.Empty);
        await Expect(schedule.Locator(".Tournament-empty-copy")).ToHaveCountAsync(0);

        await bracketFilter.SelectOptionAsync("All");
        await Expect(roundFilter).ToHaveValueAsync(string.Empty);
        await Expect(items).ToHaveCountAsync(allMatches);
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
