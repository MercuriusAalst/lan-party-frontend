using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Roster invitations surfaced through the navigation notification bell: a second actor decides
/// from a different browser context and the resulting registration state is visible to both users.
/// </summary>
[Collection(E2ECollection.Name)]
public class RegistrationNotificationPlaywrightTests : TournamentE2ETestBase
{
    public RegistrationNotificationPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task InvitedMemberCanAcceptRosterSelectionFromNotificationBell()
    {
        var (member, captain, tournamentId, teamName) = await ArrangePendingRosterAsync("bell-accept");

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/");

        await OpenNotificationBellAsync(page);
        var acceptButton = page.GetByRole(AriaRole.Button, new() { Name = "Accept roster selection" });
        await Expect(acceptButton).ToBeVisibleAsync();
        await acceptButton.ClickAsync();
        // The notification is removed only after the server confirms the decision and the
        // notification service refreshes. Wait for that visible completion before navigating.
        await Expect(acceptButton).ToHaveCountAsync(0);

        // Accepting confirms the member's roster place. The registration only becomes an active
        // participant once every member has confirmed, so the captain observes the transition.
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await Expect(page.Locator("#registration .registration-invitation")).ToHaveCountAsync(0);

        var captainContext = await App.NewAuthenticatedContextAsync(captain);
        var captainPage = await captainContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(captainPage, $"/tournaments/{tournamentId}");
        await Expect(captainPage.Locator(".participants-overview-grid")).ToContainTextAsync(teamName);
    }

    [Fact]
    public async Task InvitedMemberCanDeclineRosterSelectionFromNotificationBell()
    {
        var (member, _, tournamentId, _) = await ArrangePendingRosterAsync("bell-decline");

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/");

        await OpenNotificationBellAsync(page);
        var declineButton = page.GetByRole(AriaRole.Button, new() { Name = "Decline roster selection" });
        await Expect(declineButton).ToBeVisibleAsync();
        await declineButton.ClickAsync();
        await Expect(declineButton).ToHaveCountAsync(0);

        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await Expect(page.Locator("#registration .registration-invitation")).ToHaveCountAsync(0);
        await Expect(page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();
    }

    private async Task<(E2EPersona Member, E2EPersona Captain, Guid TournamentId, string TeamName)> ArrangePendingRosterAsync(string label)
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync($"{label}-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique($"E2E {label}"), teamSize: 2);

        var captain = await App.CreatePersonaAsync($"{label}-captain");
        var member = await App.CreatePersonaAsync($"{label}-member");
        var captainApi = App.CreateApiClient(captain);
        var teamName = TournamentE2E.Unique($"E2E {label} Squad");
        var teamId = await TournamentE2E.CreateTeamAsync(captainApi, captain.UserId!.Value, teamName);
        await TournamentE2E.InviteAndAcceptAsync(captainApi, App.CreateApiClient(member), teamId, member.UserId!.Value);
        await TournamentE2E.SubmitTeamRosterAsync(
            captainApi, tournamentId, teamId, captain.UserId!.Value, member.UserId!.Value);

        return (member, captain, tournamentId, teamName);
    }

    private static async Task OpenNotificationBellAsync(IPage page)
    {
        var bell = page.GetByRole(AriaRole.Button, new() { Name = "Notifications" });
        await Expect(bell).ToBeVisibleAsync();
        await bell.ClickAsync();
        await Expect(page.Locator(".nav-notification-dropdown")).ToBeVisibleAsync();
    }

}
