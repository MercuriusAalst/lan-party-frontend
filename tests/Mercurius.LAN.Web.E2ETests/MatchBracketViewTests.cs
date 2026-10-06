using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The bracket section of a tournament: the empty placeholder, each supported bracket
/// renderer, the two entry points that open a match dialog, and the brackets that fall
/// back to the unsupported / leaderboard rendering.
/// </summary>
[Collection(E2ECollection.Name)]
public class MatchBracketViewTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task ScheduledTournamentShowsBracketPlaceholderUntilMatchesExist()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("bracket-empty-admin"), isAdmin: true);
        var tournamentId = await TournamentE2E.CreateScheduledIndividualTournamentAsync(
            app.CreateApiClient(admin),
            TournamentE2E.Unique("E2E empty bracket"));

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var bracket = page.Locator("#tournament-bracket");
        await Expect(bracket.GetByRole(AriaRole.Heading, new() { Name = "No bracket yet" })).ToBeVisibleAsync();
        await Expect(bracket.GetByText("The bracket will appear once matches have started.")).ToBeVisibleAsync();
        await Expect(bracket.Locator(".bracket-root")).ToHaveCountAsync(0);
        await Expect(bracket.Locator(".bracket-match")).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task StartedSingleEliminationRendersEveryBracketMatchWithItsParticipants()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("bracket-se-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "bracket-se-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, "SingleElimination");

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var bracket = page.Locator("#tournament-bracket");
        await Expect(bracket.Locator(".bracket-root")).ToBeVisibleAsync();
        await Expect(bracket.Locator(".bracket-match")).ToHaveCountAsync(3);
        await Expect(bracket.GetByText("BYE")).ToHaveCountAsync(0);

        foreach (var player in players)
            await Expect(bracket.GetByText(player.Username, new() { Exact = true })).ToBeVisibleAsync();

        // The final is generated with two empty slots until the semi-finals produce winners.
        await Expect(bracket.GetByText("TBD")).ToHaveCountAsync(2);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task BracketMatchCardOpensMatchDetailsDialogWhichCanBeClosed()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("bracket-card-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "bracket-card-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, "SingleElimination");

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var dialog = await MatchE2E.OpenMatchAsync(page, players[0].Username);
        await Expect(dialog).ToContainTextAsync(players[0].Username);
        await Expect(dialog).ToContainTextAsync(players[1].Username);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();

        await MatchE2E.CloseDialogAsync(dialog);
        await Expect(page.Locator("[role='dialog'][aria-labelledby='match-dialog-title']")).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task UpcomingScheduleEntryOpensTheSameMatchDetailsDialog()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("bracket-schedule-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "bracket-schedule-player", 2);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, "SingleElimination");

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var dialog = await MatchE2E.OpenMatchFromScheduleAsync(page, players[0].Username);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = MatchE2E.AwaitingConfirmation })).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task DoubleEliminationBracketExposesUpperLowerAndGrandFinalViews()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("bracket-de-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "bracket-de-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, "DoubleElimination");

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var shell = page.Locator("#tournament-bracket .double-elimination-shell");
        await Expect(shell).ToBeVisibleAsync();

        var tabs = shell.GetByRole(AriaRole.Tablist, new() { Name = "Tournament bracket views" });
        await Expect(tabs.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^Upper bracket") })).ToBeVisibleAsync();
        await Expect(tabs.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^Lower bracket") })).ToBeVisibleAsync();
        await Expect(tabs.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^Grand final") })).ToBeVisibleAsync();

        await Expect(shell.Locator(".bracket-root")).ToBeVisibleAsync();

        await MatchE2E.ClickAsync(tabs.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^Lower bracket") }));
        await Expect(shell.Locator("#double-elimination-bracket-root")).ToBeVisibleAsync();

        await MatchE2E.ClickAsync(tabs.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^Grand final") }));
        await Expect(shell.Locator(".double-elimination-finals-card .bracket-match")).ToBeVisibleAsync();

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task RoundRobinTournamentRendersTheUnsupportedBracketNotice()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("bracket-rr-admin"), isAdmin: true);
        var players = await MatchE2E.CreatePersonasAsync(app, "bracket-rr-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(
            app, app.CreateApiClient(admin), players, "RoundRobin");

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var bracket = page.Locator("#tournament-bracket");
        await Expect(bracket.GetByText("This bracket type is not supported yet.")).ToBeVisibleAsync();
        await Expect(bracket.Locator(".bracket-root")).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }

    [Fact]
    public async Task LeaderboardTournamentReplacesTheBracketWithTheLeaderboard()
    {
        var admin = await app.CreatePersonaAsync(MatchE2E.UniqueLabel("bracket-lb-admin"), isAdmin: true);
        var adminApi = app.CreateApiClient(admin);
        var tournamentId = await TournamentE2E.CreateLeaderboardTournamentAsync(
            adminApi, TournamentE2E.Unique("E2E leaderboard bracket"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, tournamentId, "InProgress");

        var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(MatchE2E.Route(app, $"tournaments/{tournamentId}"));
        await page.WaitForInteractiveAsync();

        var bracket = page.Locator("#tournament-bracket");
        await Expect(bracket.GetByRole(AriaRole.Heading, new() { Name = "Leaderboard" })).ToBeVisibleAsync();
        await Expect(bracket.Locator(".leaderboard-shell")).ToBeVisibleAsync();
        await Expect(bracket.Locator(".bracket-root")).ToHaveCountAsync(0);
        await Expect(bracket.Locator(".bracket-match")).ToHaveCountAsync(0);

        await MatchE2E.CloseContextsAsync(app, context);
    }
}
