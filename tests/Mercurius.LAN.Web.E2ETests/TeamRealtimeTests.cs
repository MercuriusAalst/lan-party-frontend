using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class TeamRealtimeTests : E2ETestBase
{
    private readonly PlaywrightE2EFixture _app;

    public TeamRealtimeTests(PlaywrightE2EFixture app) : base(app) => _app = app;

    [Fact]
    public async Task CaptainRosterRefreshesWhenInviteeAcceptsInviteFromTheirOwnBrowser()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-rt-accept"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-rt-accept"));
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var captainContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var captainPage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, captainContext, captain, "/teams/manage");
        await Expect(captainPage.Locator(".team-detail-header p")).ToContainTextAsync("1 member");
        await Expect(TeamE2E.MemberCard(captainPage, invitee.Username)).Not.ToBeVisibleAsync();

        await using var inviteeContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var inviteePage = await TeamE2E.LoginAndOpenAsync(_app, inviteeContext, invitee, "/teams/manage");
        await Expect(inviteePage.Locator(".team-invite-row")).ToHaveCountAsync(1);

        using var captainNavigations = new TeamE2E.NavigationCounter(captainPage);
        using var inviteeNavigations = new TeamE2E.NavigationCounter(inviteePage);
        await inviteePage.ClickWhenInteractiveAsync(
            inviteePage.GetByRole(AriaRole.Button, new() { Name = $"Accept invite from {team.Name}", Exact = true }));

        await Expect(TeamE2E.MemberCard(captainPage, invitee.Username)).ToBeVisibleAsync();
        await Expect(captainPage.Locator(".team-detail-header p")).ToContainTextAsync("2 members");
        await Expect(inviteePage.GetByRole(AriaRole.Heading, new() { Name = "Your membership" })).ToBeVisibleAsync();
        Assert.Equal(0, captainNavigations.Count);
        Assert.Equal(0, inviteeNavigations.Count);
    }

    [Fact]
    public async Task CaptainRosterRefreshesWhenMemberLeavesWithoutReload()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-rt-leave"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-rt-leave"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, context, captain, "/teams/manage");
        await Expect(TeamE2E.MemberCard(page, member.Username)).ToBeVisibleAsync();
        await using var memberContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var memberPage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, memberContext, member, "/teams/manage");
        await Expect(TeamE2E.TeamSelectorItem(memberPage, team.Name)).ToBeVisibleAsync();

        using var navigations = new TeamE2E.NavigationCounter(page);
        using var memberNavigations = new TeamE2E.NavigationCounter(memberPage);
        var leaveResponse = await memberApi.DeleteAsync($"/v1/lan/teams/{team.Id}/members/me");
        Assert.True(leaveResponse.IsSuccessStatusCode);

        await Expect(TeamE2E.MemberCard(page, member.Username)).Not.ToBeVisibleAsync();
        await Expect(page.Locator(".team-detail-header p")).ToContainTextAsync("1 member");
        await Expect(memberPage.GetByText("You do not have a team yet.")).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(TeamE2E.TeamSelectorItem(memberPage, team.Name)).Not.ToBeVisibleAsync();
        Assert.Equal(0, navigations.Count);
        Assert.Equal(0, memberNavigations.Count);
    }

    [Fact]
    public async Task RemovingMemberUpdatesCaptainAndRemovedMemberWithoutReload()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-rt-remove"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-rt-remove"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));

        await using var captainContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var captainPage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, captainContext, captain, "/teams/manage");
        await Expect(TeamE2E.MemberCard(captainPage, member.Username)).ToBeVisibleAsync();

        await using var memberContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var memberPage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, memberContext, member, "/teams/manage");
        await Expect(TeamE2E.TeamSelectorItem(memberPage, team.Name)).ToBeVisibleAsync();

        using var captainNavigations = new TeamE2E.NavigationCounter(captainPage);
        using var memberNavigations = new TeamE2E.NavigationCounter(memberPage);
        var removal = await captainApi.DeleteAsync($"/v1/lan/teams/{team.Id}/members/{TeamE2E.RequireUserId(member)}");
        Assert.True(removal.IsSuccessStatusCode, $"Removal failed: {removal.StatusCode} {await removal.Content.ReadAsStringAsync()}");

        await Expect(TeamE2E.MemberCard(captainPage, member.Username)).Not.ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(captainPage.Locator(".team-detail-header p")).ToContainTextAsync("1 member");
        await Expect(memberPage.GetByText("You do not have a team yet.")).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(TeamE2E.TeamSelectorItem(memberPage, team.Name)).Not.ToBeVisibleAsync();
        Assert.Equal(0, captainNavigations.Count);
        Assert.Equal(0, memberNavigations.Count);
    }

    [Fact]
    public async Task DeletingTeamRefreshesConnectedMembersAndInviteesWithoutReload()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-rt-delete"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-rt-delete"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-rt-delete"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var captainContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var captainPage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, captainContext, captain, "/teams/manage");
        await using var memberContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var memberPage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, memberContext, member, "/teams/manage");
        await using var inviteeContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var inviteePage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, inviteeContext, invitee, "/teams/manage");

        await Expect(TeamE2E.MemberCard(captainPage, member.Username)).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelectorItem(memberPage, team.Name)).ToBeVisibleAsync();
        await Expect(inviteePage.Locator(".team-invite-row")).ToHaveCountAsync(1);
        using var captainNavigations = new TeamE2E.NavigationCounter(captainPage);
        using var memberNavigations = new TeamE2E.NavigationCounter(memberPage);
        using var inviteeNavigations = new TeamE2E.NavigationCounter(inviteePage);

        await captainPage.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(captainPage));
        await captainPage.ClickWhenInteractiveAsync(
            captainPage.Locator(".team-danger-zone").GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true }));
        var confirmation = TeamE2E.ConfirmationDialog(captainPage, "Delete team");
        await captainPage.ClickWhenInteractiveAsync(
            confirmation.GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true }));

        await Expect(captainPage.GetByText("Team deleted.")).ToBeVisibleAsync();
        await Expect(memberPage.GetByText("You do not have a team yet.")).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(TeamE2E.TeamSelectorItem(memberPage, team.Name)).Not.ToBeVisibleAsync();
        await Expect(inviteePage.Locator(".team-invite-row")).ToHaveCountAsync(0, new() { Timeout = 15000 });
        Assert.Equal(0, captainNavigations.Count);
        Assert.Equal(0, memberNavigations.Count);
        Assert.Equal(0, inviteeNavigations.Count);

    }
}
