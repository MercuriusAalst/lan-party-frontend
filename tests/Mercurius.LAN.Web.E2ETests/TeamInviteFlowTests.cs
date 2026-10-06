using System.Net;
using System.Net.Http.Json;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class TeamInviteFlowTests : E2ETestBase
{
    private readonly PlaywrightE2EFixture _app;

    public TeamInviteFlowTests(PlaywrightE2EFixture app) : base(app) => _app = app;

    [Fact]
    public async Task CaptainInvitesPlayerThenCancelsThePendingInvite()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-invite"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-invite"));
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));

        var dialog = TeamE2E.InviteDialog(page);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText($"Select a user to invite to {team.Name}.")).ToBeVisibleAsync();

        await dialog.GetByLabel("Search username").FillAsync(invitee.Username);
        var result = dialog.GetByRole(AriaRole.Option, new() { Name = invitee.Username });
        await Expect(result).ToBeVisibleAsync();
        await Expect(result).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(result);

        var sendButton = dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true });
        await Expect(sendButton).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(sendButton);

        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(page.GetByText("Invite sent.")).ToBeVisibleAsync();
        await Expect(TeamE2E.SentInvites(page)).ToHaveCountAsync(1);
        await Expect(page.Locator(".team-sent-invite-list")).ToContainTextAsync(invitee.Username);

        var pendingSent = await TeamE2E.GetSummaryAsync(captainApi);
        var sentInvite = Assert.Single(pendingSent.SentPendingInvites);

        await page.ClickWhenInteractiveAsync(page.Locator(".team-sent-invite-list").GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));

        await Expect(page.GetByText("No pending invites.")).ToBeVisibleAsync();
        await Expect(TeamE2E.SentInvites(page)).ToHaveCountAsync(0);

        var afterCancel = await TeamE2E.GetSummaryAsync(captainApi);
        Assert.Empty(afterCancel.SentPendingInvites);
        Assert.Equal("Cancelled", await TeamE2E.ReadInviteStatusAsync(_app, sentInvite.Id));
    }

    [Fact]
    public async Task InviteSearchNeedsThreeCharactersAndFlagsExistingTeammates()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-search"));
        var member = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("member-search"));
        var outsider = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("outsider-search"));
        using var captainApi = _app.CreateApiClient(captain);
        using var memberApi = _app.CreateApiClient(member);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.AddMemberAsync(captainApi, memberApi, team.Id, TeamE2E.RequireUserId(member));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));

        var dialog = TeamE2E.InviteDialog(page);
        var search = dialog.GetByLabel("Search username");
        var results = dialog.GetByRole(AriaRole.Listbox, new() { Name = "User search results" });

        await search.FillAsync("ab");
        await Expect(results).Not.ToBeVisibleAsync();

        await search.FillAsync(member.Username);
        var memberOption = dialog.GetByRole(AriaRole.Option, new() { Name = member.Username });
        await Expect(memberOption).ToBeVisibleAsync();
        await Expect(memberOption).ToBeDisabledAsync();
        await Expect(memberOption).ToContainTextAsync("Already in this team");

        await search.FillAsync(outsider.Username);
        var outsiderOption = dialog.GetByRole(AriaRole.Option, new() { Name = outsider.Username });
        await Expect(outsiderOption).ToBeVisibleAsync();
        await Expect(outsiderOption).ToBeEnabledAsync();
        await Expect(memberOption).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainCannotInvitePlayerWhoAlreadyHasAPendingInvite()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-pending"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-pending"));
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));

        var dialog = TeamE2E.InviteDialog(page);
        await dialog.GetByLabel("Search username").FillAsync(invitee.Username);
        var result = dialog.GetByRole(AriaRole.Option, new() { Name = invitee.Username });
        await Expect(result).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(result);
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true }));

        await Expect(page.GetByText(TeamE2E.GenericActionFailure)).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        await Expect(TeamE2E.SentInvites(page)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task CaptainCannotReinviteAfterThreeDeclinesInsideCooldown()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-cooldown"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-cooldown"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        var inviteeId = TeamE2E.RequireUserId(invitee);
        for (var i = 0; i < 3; i++)
            await TeamE2E.DeclineInviteAsync(captainApi, inviteeApi, team.Id, inviteeId);

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));

        var dialog = TeamE2E.InviteDialog(page);
        await dialog.GetByLabel("Search username").FillAsync(invitee.Username);
        var result = dialog.GetByRole(AriaRole.Option, new() { Name = invitee.Username });
        await Expect(result).ToBeVisibleAsync();
        await Expect(result).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(result);
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true }));

        await Expect(page.GetByText(TeamE2E.GenericActionFailure)).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        await Expect(page.GetByText("No pending invites.")).ToBeVisibleAsync();
        Assert.Empty((await TeamE2E.GetSummaryAsync(captainApi)).SentPendingInvites);
    }

    [Fact]
    public async Task InviteeAcceptsInviteFromManageTeamsAndBecomesMember()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-accept"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-accept"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, invitee, "/teams/manage");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Team invites" })).ToBeVisibleAsync();
        await Expect(page.Locator(".team-invite-row")).ToHaveCountAsync(1);

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = $"Accept invite from {team.Name}", Exact = true }));

        await Expect(page.GetByText("Invite accepted.")).ToBeVisibleAsync();
        await Expect(page.Locator(".team-invite-row")).ToHaveCountAsync(0);
        await Expect(TeamE2E.TeamSelectorItem(page, team.Name)).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your membership" })).ToBeVisibleAsync();
        await Expect(TeamE2E.TeamToolsTab(page)).Not.ToBeVisibleAsync();

        await page.ReloadAsync();
        await page.WaitForInteractiveAsync();
        await Expect(TeamE2E.TeamSelectorItem(page, team.Name)).ToBeVisibleAsync();

        var inviteeSummary = await TeamE2E.GetSummaryAsync(inviteeApi);
        Assert.Empty(inviteeSummary.ReceivedPendingInvites);
        Assert.Single(inviteeSummary.MemberTeams);
        var captainSummary = await TeamE2E.GetSummaryAsync(captainApi);
        Assert.Equal(2, captainSummary.CaptainedTeams.Single(t => t.Id == team.Id).Members.Count);
    }

    [Fact]
    public async Task InviteeDeclinesInviteAndIsNotAddedToTeam()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-decline"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-decline"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, invitee, "/teams/manage");
        await Expect(page.Locator(".team-invite-row")).ToHaveCountAsync(1);

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = $"Decline invite from {team.Name}", Exact = true }));

        await Expect(page.GetByText("Invite declined.")).ToBeVisibleAsync();
        await Expect(page.Locator(".team-invite-row")).ToHaveCountAsync(0);
        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();

        var inviteeSummary = await TeamE2E.GetSummaryAsync(inviteeApi);
        Assert.Empty(inviteeSummary.ReceivedPendingInvites);
        Assert.Empty(inviteeSummary.MemberTeams);
    }

    [Fact]
    public async Task CanceledInviteDisappearsForTheInviteeWithoutReload()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-cancel-push"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-cancel-push"));
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        var invite = await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, context, invitee, "/teams/manage");
        await Expect(page.Locator(".team-invite-row")).ToHaveCountAsync(1);

        using var navigations = new TeamE2E.NavigationCounter(page);
        var cancelResponse = await captainApi.DeleteAsync($"/v1/lan/teams/{team.Id}/invites/{invite.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        await Expect(page.Locator(".team-invite-row")).ToHaveCountAsync(0);
        Assert.Equal(0, navigations.Count);
    }

    [Fact]
    public async Task ExpiredPendingInviteIsHiddenFromTheInvitee()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-expired"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-expired"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        var invite = await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));
        await TeamE2E.ExpireInviteAsync(_app, invite.Id);

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, invitee, "/teams/manage");

        await Expect(page.Locator(".team-invite-row")).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Team invites" })).Not.ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(inviteeApi);
        Assert.Empty(summary.ReceivedPendingInvites);

        var respond = await inviteeApi.PatchAsJsonAsync($"/v1/lan/team-invites/{invite.Id}", new { accept = true });
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
    }

    [Fact]
    public async Task CaptainInviteThroughUiRaisesInviteeNotificationThatCanBeAccepted()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-bell"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-bell"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());

        await using var captainContext = await TeamE2E.TeamContext.CreateAsync(_app);
        var captainPage = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, captainContext, captain, "/teams/manage");
        await Expect(TeamE2E.TeamSelectorItem(captainPage, team.Name)).ToBeVisibleAsync();

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, context, invitee, "/");
        using var navigations = new TeamE2E.NavigationCounter(page);

        await TeamE2E.SendInviteThroughUiAsync(captainPage, invitee.Username);

        var bell = page.GetByRole(AriaRole.Button, new() { Name = "Notifications with 1 unread" });
        await Expect(bell).ToBeVisibleAsync();
        Assert.Equal(0, navigations.Count);

        await page.ClickWhenInteractiveAsync(bell);
        await Expect(page.GetByText("Team invitation")).ToBeVisibleAsync();
        await Expect(page.GetByText($"You have been invited to join {team.Name}.")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Accept invitation", Exact = true }));

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Notifications", Exact = true })).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(inviteeApi);
        Assert.Single(summary.MemberTeams);
        Assert.Equal(team.Id, summary.MemberTeams[0].Id);
        Assert.Equal(0, navigations.Count);
    }

    [Fact]
    public async Task InviteNotificationBadgeLetsInviteeDecline()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-bell-decline"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-bell-decline"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());

        // Seed the pending invite before the invitee page initialises its notifications, matching
        // the mark-read/canceled siblings. Opening the page first hydrates the bell from a summary
        // that ran before this invite existed, and nothing re-reads it afterwards.
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, context, invitee, "/");

        var bell = page.GetByRole(AriaRole.Button, new() { Name = "Notifications with 1 unread" });
        await Expect(bell).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(bell);

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Decline invitation", Exact = true }));

        await Expect(page.GetByText("No notifications.")).ToBeVisibleAsync();

        var summary = await TeamE2E.GetSummaryAsync(inviteeApi);
        Assert.Empty(summary.ReceivedPendingInvites);
        Assert.Empty(summary.MemberTeams);
    }

    [Fact]
    public async Task InviteDialogShowsEmptyResultMessageAndCanBeCanceled()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-invite-empty"));
        using var api = _app.CreateApiClient(captain);
        await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));

        var dialog = TeamE2E.InviteDialog(page);
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true })).ToBeDisabledAsync();
        await dialog.GetByLabel("Search username").FillAsync($"zzz-{Guid.NewGuid():N}");
        await Expect(dialog.GetByText("No matching players found.")).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true })).ToBeDisabledAsync();

        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));
        await Expect(dialog).Not.ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        await Expect(page.GetByText("No pending invites.")).ToBeVisibleAsync();
        Assert.Empty((await TeamE2E.GetSummaryAsync(api)).SentPendingInvites);
    }

    [Fact]
    public async Task ExpiredPendingInviteIsSupersededWhenCaptainInvitesAgain()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-reinvite-expired"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-reinvite-expired"));
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        var inviteeId = TeamE2E.RequireUserId(invitee);
        var expiredInvite = await TeamE2E.InviteAsync(captainApi, team.Id, inviteeId);
        await TeamE2E.ExpireInviteAsync(_app, expiredInvite.Id);

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));

        var dialog = TeamE2E.InviteDialog(page);
        await dialog.GetByLabel("Search username").FillAsync(invitee.Username);
        var result = dialog.GetByRole(AriaRole.Option, new() { Name = invitee.Username });
        await Expect(result).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(result);
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true }));

        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(page.GetByText("Invite sent.")).ToBeVisibleAsync();
        Assert.Equal("Expired", await TeamE2E.ReadInviteStatusAsync(_app, expiredInvite.Id));
        Assert.Equal(2, await TeamE2E.CountInvitesAsync(_app, team.Id, inviteeId));
        Assert.Single((await TeamE2E.GetSummaryAsync(captainApi)).SentPendingInvites);
    }

    [Fact]
    public async Task CanceledInviteCanBeSentAgain()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-resend"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-resend"));
        using var captainApi = _app.CreateApiClient(captain);
        using var inviteeApi = _app.CreateApiClient(invitee);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.TeamToolsTab(page));
        await page.ClickWhenInteractiveAsync(page.Locator(".team-sent-invite-list").GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));
        await Expect(page.GetByText("No pending invites.")).ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));
        var dialog = TeamE2E.InviteDialog(page);
        await dialog.GetByLabel("Search username").FillAsync(invitee.Username);
        var result = dialog.GetByRole(AriaRole.Option, new() { Name = invitee.Username });
        await Expect(result).ToBeEnabledAsync();
        await page.ClickWhenInteractiveAsync(result);
        await page.ClickWhenInteractiveAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true }));

        await Expect(page.GetByText("Invite sent.")).ToBeVisibleAsync();
        await Expect(TeamE2E.SentInvites(page)).ToHaveCountAsync(1);

        var summary = await TeamE2E.GetSummaryAsync(inviteeApi);
        Assert.Single(summary.ReceivedPendingInvites);
    }

    [Fact]
    public async Task InviteNotificationCanBeMarkedReadAndDismissed()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-bell-read"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-bell-read"));
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, context, invitee, "/");

        var unreadBell = page.GetByRole(AriaRole.Button, new() { Name = "Notifications with 1 unread" });
        await Expect(unreadBell).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(unreadBell);
        await Expect(page.GetByText("Team invitation")).ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Mark read", Exact = true }));

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Notifications", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Team invitation")).ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Dismiss notification", Exact = true }));

        await Expect(page.GetByText("No notifications.")).ToBeVisibleAsync();
        await Expect(page.GetByText("Team invitation")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task AcceptingAnInviteThatWasCanceledOutOfBandShowsNotificationActionFailure()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-bell-stale"));
        var invitee = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("invitee-bell-stale"));
        using var captainApi = _app.CreateApiClient(captain);
        var team = await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());
        var invite = await TeamE2E.InviteAsync(captainApi, team.Id, TeamE2E.RequireUserId(invitee));

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenWithLiveUpdatesAsync(_app, context, invitee, "/");
        var unreadBell = page.GetByRole(AriaRole.Button, new() { Name = "Notifications with 1 unread" });
        await Expect(unreadBell).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(unreadBell);
        await Expect(page.GetByText("Team invitation")).ToBeVisibleAsync();

        await TeamE2E.CancelInviteWithoutEventAsync(_app, invite.Id);

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Accept invitation", Exact = true }));

        await Expect(page.Locator(".nav-notification-error"))
            .ToHaveTextAsync("That invitation could not be updated. Please try again.");
        await Expect(page.GetByText("Team invitation")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task InviteSearchShowsUnavailableMessageWhenTheBackendSearchFails()
    {
        var captain = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("captain-search-fault"));
        var candidate = await _app.CreatePersonaAsync(TeamE2E.UniqueLabel("candidate-search-fault"));
        using var captainApi = _app.CreateApiClient(captain);
        await TeamE2E.CreateTeamAsync(captainApi, TeamE2E.UniqueTeamName());

        await using var context = await TeamE2E.TeamContext.CreateAsync(_app);
        var page = await TeamE2E.LoginAndOpenAsync(_app, context, captain, "/teams/manage");
        await page.ClickWhenInteractiveAsync(TeamE2E.InvitePanelButton(page));

        var dialog = TeamE2E.InviteDialog(page);
        await Expect(dialog).ToBeVisibleAsync();

        // The user search reads identity.users; hiding the table makes the real server-side search
        // fail while the dialog is already open, which is the only way to reach this UI branch.
        await using (await DatabaseReadFault.InstallAsync(_app, "users"))
        {
            await dialog.GetByLabel("Search username").FillAsync(candidate.Username);
            await Expect(dialog.Locator(".team-dialog-alert"))
                .ToHaveTextAsync("User search is unavailable right now.");
        }

        await Expect(dialog.GetByRole(AriaRole.Listbox, new() { Name = "User search results" })).Not.ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true })).ToBeDisabledAsync();
    }
}
