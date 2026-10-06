using System.Net;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class TeamDeleteTests : E2ETestBase
{
    private readonly PlaywrightE2EFixture _app;

    public TeamDeleteTests(PlaywrightE2EFixture app) : base(app) => _app = app;

    [Fact]
    public async Task CaptainDeletesTeamAfterConfirmingAndProfileDisappears()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-delete"));
        using var api = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await Expect(TeamE2E.TeamSelectorItem(page, team.Name)).ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        await page.ClickWhenInteractiveAsync(page.Locator(".team-danger-zone").GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true }));

        var dialog = TeamE2E.ConfirmationDialog(page, "Delete team");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText($"Delete {team.Name}? This removes the team from your managed teams.")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true }));

        await Expect(page.GetByText("Team deleted.")).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelectorItem(page, team.Name)).Not.ToBeVisibleAsync();
        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(api);
        Assert.Empty(summary.CaptainedTeams);

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();

        await page.GotoAsync(new Uri(new Uri(_app.BaseUrl), TeamE2E.TeamProfilePath(team.Name)).ToString());
        await page.WaitForInteractiveAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Team not found" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainCanCancelTeamDeletion()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-delete-cancel"));
        using var api = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        await page.ClickWhenInteractiveAsync(page.Locator(".team-danger-zone").GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true }));

        var dialog = TeamE2E.ConfirmationDialog(page, "Delete team");
        await Expect(dialog).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));

        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelectorItem(page, team.Name)).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(api);
        Assert.Single(summary.CaptainedTeams);

        var publicProfile = await _app.Api.GetAsync($"/v1/lan/public/teams/{Uri.EscapeDataString(team.Name)}");
        Assert.Equal(HttpStatusCode.OK, publicProfile.StatusCode);
    }

    [Fact]
    public async Task MemberCannotDeleteTeamThroughTheApi()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-delete-guard"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-delete-guard"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));

        var response = await memberApi.DeleteAsync($"/v1/lan/teams/{team.Id}");

        // EnsureCaptain throws UnauthorizedAccessException, which the API exception handler maps to 401.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var summary = await TeamE2E.GetSummaryAsync(captainApi);
        Assert.Single(summary.CaptainedTeams);
    }

    [Fact]
    public async Task CaptainCannotDeleteTeamThatIsPlayingInATournament()
    {
        var admin = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("admin-delete-blocked"), isAdmin: true);
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-delete-blocked"));
        var opponent = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("opponent-delete-blocked"));
        using var adminApi = _app.CreateApiClient(admin);
        var teamName = TeamE2E.UniqueTeamName();
        await MatchE2E.StartTeamTournamentAsync(
            _app,
            adminApi,
            [(captain, teamName), (opponent, TeamE2E.UniqueTeamName())],
            teamSize: 1);

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await TeamE2E.TeamToolsTab(page).ClickWhenInteractiveAsync();
        await page.ClickWhenInteractiveAsync(
            page.Locator(".team-danger-zone").GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true }));

        var dialog = TeamE2E.ConfirmationDialog(page, "Delete team");
        await Expect(dialog).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true }));

        await Expect(page.GetByText(TeamE2E.GenericActionFailure)).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelectorItem(page, teamName)).ToBeVisibleAsync();

        using var captainApi = _app.CreateApiClient(captain);
        Assert.Single((await TeamE2E.GetSummaryAsync(captainApi)).CaptainedTeams);
    }
}
