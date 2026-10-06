using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class RegistrationTeamPlaywrightTests : TournamentE2ETestBase
{
    public RegistrationTeamPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task CaptainSubmitsRosterAndInvitedMemberAcceptingActivatesTheRegistration()
    {
        var (captain, member, teamId, teamName, tournamentId) = await ArrangeTeamAsync("accept");

        var captainContext = await App.NewAuthenticatedContextAsync(captain);
        var captainPage = await captainContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(captainPage, $"/tournaments/{tournamentId}");

        await SubmitTeamRosterThroughUiAsync(captainPage, teamName);

        // Roster submitted: the registration is pending until the invited member confirms.
        await Expect(captainPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();
        await Expect(captainPage.Locator(".participants-state-card")).ToContainTextAsync("No participants yet");

        var memberContext = await App.NewAuthenticatedContextAsync(member);
        var memberPage = await memberContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(memberPage, $"/tournaments/{tournamentId}");

        var invitation = memberPage.Locator("#registration .registration-invitation");
        await Expect(invitation).ToContainTextAsync(TournamentE2E.Text.YouWereSelected);
        await invitation.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.AcceptRosterPlace }).ClickAsync();

        // The accept round-trip and the current-user refresh run server-side after the click
        // returns, so wait for the refreshed panel; reloading earlier reads pre-accept state.
        await Expect(memberPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();

        await memberPage.ReloadAsync();
        await Expect(memberPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();

        // Once every roster member has confirmed, the team appears as an active participant.
        await captainPage.ReloadAsync();
        await Expect(captainPage.Locator(".participants-overview-grid")).ToContainTextAsync(teamName);
    }

    [Fact]
    public async Task InvitedMemberDecliningRemovesThemFromTheRoster()
    {
        var (captain, member, _, teamName, tournamentId) = await ArrangeTeamAsync("decline");

        var captainContext = await App.NewAuthenticatedContextAsync(captain);
        var captainPage = await captainContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(captainPage, $"/tournaments/{tournamentId}");
        await SubmitTeamRosterThroughUiAsync(captainPage, teamName);

        var memberContext = await App.NewAuthenticatedContextAsync(member);
        var memberPage = await memberContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(memberPage, $"/tournaments/{tournamentId}");

        var invitation = memberPage.Locator("#registration .registration-invitation");
        await Expect(invitation).ToContainTextAsync(TournamentE2E.Text.YouWereSelected);
        await invitation.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.DeclineRosterPlace }).ClickAsync();

        // The decline round-trip and the current-user refresh run server-side after the click
        // returns, so wait for the refreshed panel; reloading earlier reads pre-decline state.
        await Expect(memberPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();

        await memberPage.ReloadAsync();
        await Expect(memberPage.Locator("#registration .registration-invitation")).ToHaveCountAsync(0);
        await Expect(memberPage.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainSeesRosterEligibilityErrorWhenEligibilityCheckFails()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("roster-fault-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique("E2E Roster Fault"), teamSize: 2);

        var captain = await App.CreatePersonaAsync("roster-fault-captain");
        var member = await App.CreatePersonaAsync("roster-fault-member");
        var captainApi = App.CreateApiClient(captain);
        var teamName = TournamentE2E.Unique("E2E Roster Fault Squad");
        var teamId = await TournamentE2E.CreateTeamAsync(captainApi, captain.UserId!.Value, teamName);
        await TournamentE2E.InviteAndAcceptAsync(captainApi, App.CreateApiClient(member), teamId, member.UserId!.Value);

        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();

        var dialog = page.Locator("div.registration-dialog");
        await dialog.GetByRole(AriaRole.Option).Filter(new() { HasText = teamName }).ClickAsync();
        await AdvanceTeamStepAsync(dialog, "Choose roster");

        var memberCheckbox = dialog.Locator("input[type=\"checkbox\"]").First;
        await Expect(memberCheckbox).ToBeVisibleAsync();

        // Toggling a roster member triggers a server-side eligibility check; hiding the tournament
        // table makes it fail, which must surface as an error instead of silently allowing a save.
        await using (await DatabaseReadFault.InstallAsync(App, "tournaments"))
        {
            // Force a real change event: the component may have auto-filled the roster already.
            if (await memberCheckbox.IsCheckedAsync())
                await memberCheckbox.UncheckAsync();
            await memberCheckbox.CheckAsync();
            await Expect(dialog.Locator("[role=\"alert\"]"))
                .ToContainTextAsync(TournamentE2E.Text.RosterEligibilityUnavailable);
        }
    }

    [Fact]
    public async Task CaptainSeesTeamContextUnavailableMessageWhileTeamDataIsFaulted()
    {
        var (captain, _, _, teamName, tournamentId) = await ArrangeTeamAsync("context-fault");

        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");

        // The captained-teams summary call happens server-side, so hiding the teams table makes the
        // registration dialog degrade to its "teams could not be loaded" state.
        await using (await DatabaseReadFault.InstallAsync(App, "teams"))
        {
            await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
            await Expect(page.Locator("div.registration-dialog")
                    .GetByText(TournamentE2E.Text.TeamsUnavailable))
                .ToBeVisibleAsync();
        }

        // Once the data is reachable again a fresh view offers the captained team.
        await TournamentE2E.ReloadInteractiveAsync(page);
        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        await Expect(page.Locator("div.registration-dialog")
                .GetByRole(AriaRole.Option).Filter(new() { HasText = teamName }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task CaptainCannotConfirmTeamRegistrationUntilTheRosterIsComplete()
    {
        var (captain, _, _, teamName, tournamentId) = await ArrangeTeamAsync("incomplete");

        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");

        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Option).Filter(new() { HasText = teamName }).ClickAsync();
        await AdvanceTeamStepAsync(dialog, "Choose roster");

        var memberCheckbox = dialog.Locator("input[type=\"checkbox\"]").First;
        await Expect(memberCheckbox).ToBeVisibleAsync();
        var review = dialog.GetByRole(AriaRole.Button, new() { Name = "Review roster" });

        await memberCheckbox.UncheckAsync();
        await Expect(review).ToBeDisabledAsync();

        await memberCheckbox.CheckAsync();
        await Expect(review).ToBeEnabledAsync();

        await review.ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Confirm team registration" })).ToBeEnabledAsync();
    }

    private async Task<(E2EPersona Captain, E2EPersona Member, Guid TeamId, string TeamName, Guid TournamentId)> ArrangeTeamAsync(string label)
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync($"team-{label}-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique($"E2E Team {label}"), teamSize: 2);

        var captain = await App.CreatePersonaAsync($"team-{label}-captain");
        var member = await App.CreatePersonaAsync($"team-{label}-member");
        var captainApi = App.CreateApiClient(captain);
        var teamName = TournamentE2E.Unique($"E2E Squad {label}");
        var teamId = await TournamentE2E.CreateTeamAsync(captainApi, captain.UserId!.Value, teamName);
        await TournamentE2E.InviteAndAcceptAsync(captainApi, App.CreateApiClient(member), teamId, member.UserId!.Value);

        return (captain, member, teamId, teamName, tournamentId);
    }

    private static async Task SubmitTeamRosterThroughUiAsync(IPage page, string teamName)
    {
        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Option).Filter(new() { HasText = teamName }).ClickAsync();
        await AdvanceTeamStepAsync(dialog, "Choose roster");

        var memberCheckbox = dialog.Locator("input[type=\"checkbox\"]").First;
        await Expect(memberCheckbox).ToBeVisibleAsync();
        if (!await memberCheckbox.IsCheckedAsync())
            await memberCheckbox.CheckAsync();

        await AdvanceTeamStepAsync(dialog, "Review roster");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Confirm team registration" }).ClickAsync();

        // A successful roster submit keeps the dialog open, so close it through the production close
        // control before the caller asserts the panel state.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close registration dialog" }).ClickAsync();
        await Expect(page.Locator("div.registration-dialog")).ToHaveCountAsync(0);
    }

    /// <summary>Waits for a stepper navigation button to become enabled, then advances the step.</summary>
    private static async Task AdvanceTeamStepAsync(ILocator dialog, string buttonName)
    {
        var button = dialog.GetByRole(AriaRole.Button, new() { Name = buttonName });
        await Expect(button).ToBeEnabledAsync();
        await button.ClickAsync();
    }
}
