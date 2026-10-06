using System.Net;
using System.Net.Http.Json;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class TeamAuthorizationTests : E2ETestBase
{
    private readonly PlaywrightE2EFixture _app;

    public TeamAuthorizationTests(PlaywrightE2EFixture app) : base(app) => _app = app;

    [Fact]
    public async Task MemberOnlySeesRosterAndMembershipControls()
    {
        var (_, member, _, team) = await CreateTeamWithMemberAsync("member-view");

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, member, "/teams/manage");

        await Expect(TeamE2E.TeamSelectorItem(page, team.Name)).ToBeVisibleAsync();
        await Expect(TeamE2E.MembersTab(page)).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamToolsTab(page)).Not.ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Invite player", Exact = true })).Not.ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Delete team", Exact = true })).Not.ToBeVisibleAsync();
        await Expect(page.Locator(".team-member-remove-button")).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Leave team", Exact = true })).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamSelector(page).GetByText("Captain")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task MemberCannotUseCaptainMutationsThroughTheApi()
    {
        var (captain, member, captainApi, team) = await CreateTeamWithMemberAsync("member-api");
        using var memberApi = _app.CreateApiClient(member);
        var captainId = TeamE2E.RequireUserId(captain);
        var memberId = TeamE2E.RequireUserId(member);

        var invite = await memberApi.PostAsJsonAsync($"/v1/lan/teams/{team.Id}/invites", new { userId = captainId });
        var transfer = await memberApi.PutAsJsonAsync($"/v1/lan/teams/{team.Id}/captain", new { newCaptainUserId = memberId });
        var remove = await memberApi.DeleteAsync($"/v1/lan/teams/{team.Id}/members/{captainId}");
        var removeLogo = await memberApi.DeleteAsync($"/v1/lan/teams/{team.Id}/logo");

        // EnsureCaptain throws UnauthorizedAccessException, which the API exception handler maps to 401.
        Assert.Equal(HttpStatusCode.Unauthorized, invite.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, transfer.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, remove.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, removeLogo.StatusCode);

        var summary = await TeamE2E.GetSummaryAsync(captainApi);
        var managed = Assert.Single(summary.CaptainedTeams);
        Assert.Equal(captainId, managed.CaptainUserId);
        Assert.Equal(2, managed.Members.Count);
    }

    [Fact]
    public async Task MemberCannotCancelSomeoneElsesInviteThroughTheApi()
    {
        var (captain, member, captainApi, team) = await CreateTeamWithMemberAsync("member-cancel-invite");
        var outsider = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("outsider-cancel-invite"));
        using var memberApi = _app.CreateApiClient(member);
        var invite = await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(outsider));

        var response = await memberApi.DeleteAsync($"/v1/lan/teams/{team.Id}/invites/{invite.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var summary = await TeamE2E.GetSummaryAsync(captainApi);
        Assert.Single(summary.SentPendingInvites);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadTeamManagementData()
    {
        var summary = await _app.Api.GetAsync("/v1/lan/teams/me/summary");
        var invites = await _app.Api.GetAsync("/v1/lan/teams/me/invites");
        var create = await _app.Api.PostAsJsonAsync("/v1/lan/teams", new { name = TeamE2E.UniqueTeamName() });

        Assert.Equal(HttpStatusCode.Unauthorized, summary.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invites.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Fact]
    public async Task StrangerCannotReplyToSomeoneElsesInvite()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-stranger"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-stranger"));
        var stranger = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("stranger-invite"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        using var strangerApi = _app.CreateApiClient(stranger);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        var invite = await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        var response = await strangerApi.PatchAsJsonAsync($"/v1/lan/team-invites/{invite.Id}", new { accept = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var inviteeSummary = await TeamE2E.GetSummaryAsync(inviteeApi);
        Assert.Single(inviteeSummary.ReceivedPendingInvites);
    }

    [Fact]
    public async Task MemberCannotLeaveOnBehalfOfAnotherMember()
    {
        var (_, member, _, team) = await CreateTeamWithMemberAsync("member-leave-other");
        var other = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("other-leave"));
        using var otherApi = _app.CreateApiClient(other);

        var response = await otherApi.DeleteAsync($"/v1/lan/teams/{team.Id}/members/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var memberApi = _app.CreateApiClient(member);
        Assert.Single((await TeamE2E.GetSummaryAsync(memberApi)).MemberTeams);
    }

    private async Task<(E2EPersona Captain, E2EPersona Member, HttpClient CaptainApi, TeamE2E.TeamManagementSummary Team)>
        CreateTeamWithMemberAsync(string label)
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel($"captain-{label}"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel($"member-{label}"));
        var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));
        return (captain, member, captainApi, team);
    }
}
