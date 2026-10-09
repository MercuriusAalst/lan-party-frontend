using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class TeamManagementPageTests : E2ETestBase
{
    private readonly PlaywrightE2EFixture _app;

    public TeamManagementPageTests(PlaywrightE2EFixture app) : base(app) => _app = app;

    [Fact]
    public async Task AnonymousVisitorIsSentToSignInFromTeamManagement()
    {
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await context.NewPageAsync();

        await page.GotoAsync(new Uri(new Uri(_app.BaseUrl), "/teams/manage").ToString());

        await Expect(page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Mercurius E2E sign in"));
        await Expect(page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/teams/manage$"));
    }

    [Fact]
    public async Task MemberWithoutTeamSeesEmptyWorkspaceState()
    {
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-empty"));
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);

        var page = await TeamE2E.LoginAndOpenAsync(_app, context, member, "/teams/manage");

        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Start with a team" })).ToBeVisibleAsync();
        await Expect(page.GetByText("Create a team or accept an invite to see your roster here.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Team invites" })).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainCreatesTeamThroughDialogAndItSurvivesReload()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-create"));
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        var teamName = TeamE2E.UniqueTeamName();

        var selector = TeamE2E.TeamSelector(page);
        await page.ClickWhenInteractiveAsync(selector.GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));

        var dialog = TeamE2E.CreateTeamDialog(page);
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByLabel("Team name").FillAsync(teamName);
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));

        await Expect(page.GetByText("Team created.")).ToBeVisibleAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelectorItem(page, teamName)).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = teamName })).ToBeVisibleAsync();

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();

        await Expect(TeamE2E.TeamSelectorItem(page, teamName)).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelector(page).GetByText("Captain")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CreateTeamDialogRejectsEmptyNameAndKeepsTeamListUnchanged()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-validate"));
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");

        var selector = TeamE2E.TeamSelector(page);
        await page.ClickWhenInteractiveAsync(selector.GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));

        var dialog = TeamE2E.CreateTeamDialog(page);
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));

        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Team name is required.")).ToBeVisibleAsync();
        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();

        using var api = _app.CreateApiClient(captain);
        var summary = await TeamE2E.GetSummaryAsync(api);
        Assert.Empty(summary.CaptainedTeams);
    }

    [Fact]
    public async Task CreateTeamDialogCanBeCanceledWithoutCreatingTeam()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-cancel"));
        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");

        await page.ClickWhenInteractiveAsync(TeamE2E.TeamSelector(page).GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));
        var dialog = TeamE2E.CreateTeamDialog(page);
        await dialog.GetByLabel("Team name").FillAsync(TeamE2E.UniqueTeamName());
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));

        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();

        using var api = _app.CreateApiClient(captain);
        var summary = await TeamE2E.GetSummaryAsync(api);
        Assert.Empty(summary.CaptainedTeams);
    }

    [Fact]
    public async Task DuplicateTeamNameIsRejectedAndDialogStaysOpen()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-duplicate"));
        using var api = _app.CreateApiClient(captain);
        var teamName = TeamE2E.UniqueTeamName();
        await TeamE2E.CreateTeamAsync(api, teamName);

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");

        await page.ClickWhenInteractiveAsync(TeamE2E.TeamSelector(page).GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));
        var dialog = TeamE2E.CreateTeamDialog(page);
        await dialog.GetByLabel("Team name").FillAsync(teamName);
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));

        await Expect(dialog).ToBeVisibleAsync();
        await Expect(page.GetByText(TeamE2E.GenericActionFailure)).ToBeVisibleAsync();
        await Expect(page.Locator(".team-selector-list [role=listitem]")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task CaptainCannotCreateMoreThanTwoTeams()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-limit"));
        using var api = _app.CreateApiClient(captain);
        await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());
        await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await Expect(page.Locator(".team-selector-list [role=listitem]")).ToHaveCountAsync(2);

        await page.ClickWhenInteractiveAsync(TeamE2E.TeamSelector(page).GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));
        var dialog = TeamE2E.CreateTeamDialog(page);
        await dialog.GetByLabel("Team name").FillAsync(TeamE2E.UniqueTeamName());
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Create team", Exact = true }));

        await Expect(page.GetByText(TeamE2E.GenericActionFailure)).ToBeVisibleAsync();
        await Expect(page.Locator(".team-selector-list [role=listitem]")).ToHaveCountAsync(2);

        var summary = await TeamE2E.GetSummaryAsync(api);
        Assert.Equal(2, summary.CaptainedTeams.Count);
    }

    [Fact]
    public async Task CaptainSeesTeamToolsWhileMemberSeesMembershipOnly()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-tabs"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-tabs"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));

        await using var captainContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var captainPage = await TeamE2E.LoginAndOpenAsync(_app, captainContext, captain, "/teams/manage");
        await Expect(TeamE2E.MembersTab(captainPage)).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamToolsTab(captainPage)).ToBeVisibleAsync();
        await Expect(TeamE2E.MemberCard(captainPage, member.Username)).ToBeVisibleAsync();

        await using var memberContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var memberPage = await TeamE2E.LoginAndOpenAsync(_app, memberContext, member, "/teams/manage");
        await Expect(TeamE2E.MembersTab(memberPage)).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamToolsTab(memberPage)).Not.ToBeVisibleAsync();
        await Expect(memberPage.GetByRole(AriaRole.Heading, new() { Name = "Your membership" })).ToBeVisibleAsync();
        await Expect(memberPage.GetByRole(AriaRole.Button, new() { Name = "Leave team", Exact = true })).ToBeVisibleAsync();
        await Expect(memberPage.GetByRole(AriaRole.Button, new() { Name = "Invite player", Exact = true })).Not.ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelector(memberPage).GetByText("Captain")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task TeammateCardOpensPublicPlayerDetailsDialog()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-dialog"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-dialog"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");

        await page.ClickWhenInteractiveAsync(TeamE2E.MemberCard(page, member.Username));

        var dialog = page.GetByRole(AriaRole.Dialog).Filter(new() { HasText = "Player details" });
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Only details this player has shared are shown.")).ToBeVisibleAsync();
        // The username is both the dialog title (aria-labelledby) and the Username detail row,
        // so assert the title by role instead of a plain text match, which resolves to both.
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = member.Username })).ToBeVisibleAsync();
        await Expect(dialog.Locator(".user-info-dialog-details dd").GetByText(member.Username)).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Close player details", Exact = true }));
        await Expect(dialog).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task TeamManagementLoadFailureShowsUnavailableStateAndRecovers()
    {
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("team-manage-fault"));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);

        var fault = await DatabaseReadFault.InstallAsync(_app, "teams");
        try
        {
            var page = await TeamE2E.LoginAndOpenAsync(_app, context, member, "/teams/manage");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Team management unavailable" }))
                .ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Browse Tournaments" })).ToBeVisibleAsync();

            // The StatusPage retry is an in-place callback, so recovery reruns the team summary load
            // in the current circuit once the team table is visible again.
            await fault.DisposeAsync();

            await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Try again" }));

            await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Start with a team" })).ToBeVisibleAsync();
        }
        finally
        {
            await fault.DisposeAsync();
        }
    }
}
