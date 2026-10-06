using System.Net;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class TeamPublicProfileTests : E2ETestBase
{
    private readonly PlaywrightE2EFixture _app;

    public TeamPublicProfileTests(PlaywrightE2EFixture app) : base(app) => _app = app;

    [Fact]
    public async Task AnonymousVisitorSeesRosterWithCaptainBadge()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-public"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-public"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.OpenAnonymousAsync(_app, context, TeamE2E.TeamProfilePath(team.Name));

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = team.Name })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Members" })).ToBeVisibleAsync();
        await Expect(page.GetByText("Members are shown by public username only.")).ToBeVisibleAsync();

        var roster = page.Locator(".public-team-member-list li");
        await Expect(roster).ToHaveCountAsync(2);
        await Expect(page.Locator(".public-team-member-list")).ToContainTextAsync(captain.Username);
        await Expect(page.Locator(".public-team-member-list")).ToContainTextAsync(member.Username);
        await Expect(page.Locator(".public-team-captain-badge")).ToHaveCountAsync(1);

        var captainRow = page.Locator(".public-team-member", new() { HasText = captain.Username });
        await Expect(captainRow.Locator(".public-team-captain-badge")).ToHaveTextAsync("Captain");
    }

    [Fact]
    public async Task TeamWithoutRegistrationsShowsEmptyTournamentState()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-public-tournaments"));
        using var api = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.OpenAnonymousAsync(_app, context, TeamE2E.TeamProfilePath(team.Name));

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Playing in" })).ToBeVisibleAsync();
        await Expect(page.GetByText("This team is not currently listed in any tournaments.")).ToBeVisibleAsync();
        await Expect(page.Locator(".public-team-tournament-list li")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task UnknownTeamNameShowsNotFoundStatusPage()
    {
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.OpenAnonymousAsync(_app, context, TeamE2E.TeamProfilePath($"missing-team-{Guid.NewGuid():N}"));

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Team not found" })).ToBeVisibleAsync();
        await Expect(page.GetByText("We could not find a public profile for that team.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Browse Tournaments" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PublicTeamLookupReturnsNotFoundForUnknownTeam()
    {
        var response = await _app.Api.GetAsync($"/v1/lan/public/teams/missing-team-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TeamProfileListsAndLinksTournamentsTheTeamIsRegisteredIn()
    {
        var admin = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("admin-team-tournament"), isAdmin: true);
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-team-tournament"));
        using var adminApi = _app.CreateApiClient(admin);
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        var tournamentName = TournamentE2E.Unique("E2E Team Profile Cup");
        var tournamentId = await TournamentE2E.CreateScheduledTeamTournamentAsync(adminApi, tournamentName, teamSize: 1);
        await TournamentE2E.SubmitTeamRosterAsync(captainApi, tournamentId, team.Id, TeamE2E.RequireUserId(captain));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.OpenAnonymousAsync(_app, context, TeamE2E.TeamProfilePath(team.Name));

        await Expect(page.Locator(".public-team-tournament-list li")).ToHaveCountAsync(1);
        await Expect(page.Locator(".public-team-tournament-link")).ToContainTextAsync(tournamentName);

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Link, new() { Name = tournamentName }));
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(tournamentId.ToString()));
    }

    [Fact]
    public async Task TeamProfileMatchHistoryMovesFromUpcomingToCompleted()
    {
        var admin = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("admin-team-matches"), isAdmin: true);
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-team-matches"));
        var opponentCaptain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-team-opponent"));
        using var adminApi = _app.CreateApiClient(admin);
        var teamName = TeamE2E.UniqueTeamName();
        var opponentTeamName = TeamE2E.UniqueTeamName();
        var tournamentId = await MatchE2E.StartTeamTournamentAsync(
            _app,
            adminApi,
            [(captain, teamName), (opponentCaptain, opponentTeamName)],
            teamSize: 1);
        var tournamentName = (await TournamentE2E.ReadJsonAsync(
            await _app.Api.GetAsync($"v1/lan/tournaments/{tournamentId}"))).GetProperty("name").GetString()!;

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.OpenAnonymousAsync(_app, context, TeamE2E.TeamProfilePath(teamName));

        var previousGroup = page.Locator("section[aria-labelledby='public-profile-previous-match-heading']");
        var upcomingGroup = page.Locator("section[aria-labelledby='public-profile-upcoming-match-heading']");
        await Expect(upcomingGroup.Locator(".public-profile-match-summary-card")).ToHaveCountAsync(1);
        await Expect(upcomingGroup.Locator("h3")).ToHaveTextAsync(tournamentName);
        await Expect(previousGroup.GetByText("No completed match is listed yet.")).ToBeVisibleAsync();

        var match = Assert.Single(await MatchE2E.GetMatchesAsync(_app.Api, tournamentId));
        await MatchE2E.CompleteMatchViaApiAsync(_app, match.Id, captain, opponentCaptain, 1, 0);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();

        await Expect(previousGroup.Locator(".public-profile-match-summary-card")).ToHaveCountAsync(1);
        await Expect(previousGroup.Locator("h3")).ToHaveTextAsync(tournamentName);
        await Expect(previousGroup.Locator(".public-profile-match-summary-opponent")).ToContainTextAsync(opponentTeamName);
        await Expect(previousGroup.Locator(".public-profile-match-summary-result")).ToContainTextAsync("Score");
        await Expect(upcomingGroup.GetByText("No upcoming match is scheduled.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PublicTeamProfileLoadFailureShowsUnavailableStateAndRecovers()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-public-fault"));
        using var api = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());
        var teamPath = TeamE2E.TeamProfilePath(team.Name);

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);

        await using (var fault = await DatabaseReadFault.InstallAsync(_app, "teams"))
        {
            var page = await TeamE2E.OpenAnonymousAsync(_app, context, teamPath);

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Team unavailable" }))
                .ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(page.GetByText("This team profile could not be loaded right now.")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Browse Tournaments" })).ToBeVisibleAsync();
        }

        // This error state intentionally offers no inline retry, so the visitor recovers by reloading
        // the public profile once the team table is available again.
        var recovered = await TeamE2E.OpenAnonymousAsync(_app, context, teamPath);

        await Expect(recovered.GetByRole(AriaRole.Heading, new() { Name = team.Name }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(recovered.GetByRole(AriaRole.Heading, new() { Name = "Members" })).ToBeVisibleAsync();
    }
}
