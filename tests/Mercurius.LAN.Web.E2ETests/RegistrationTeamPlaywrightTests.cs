using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class RegistrationTeamPlaywrightTests : TournamentE2ETestBase
{
    public RegistrationTeamPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task CaptainWithoutTeamsCanOpenAndCancelTeamCreationFromRegistration()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-empty-team-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique("E2E Empty Team"), teamSize: 2);
        var captain = await App.CreatePersonaAsync("reg-empty-team-captain");
        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");

        var registration = page.Locator("#registration");
        await registration.GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = "You don't captain a team yet" })).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Create your own team and invite players to participate in this tournament.")).ToBeVisibleAsync();
        await CaptureEmptyTeamStateAsync(page, "desktop-light", 1440, 900, dark: false);
        await CaptureEmptyTeamStateAsync(page, "mobile-light", 390, 844, dark: false);
        await CaptureEmptyTeamStateAsync(page, "desktop-dark", 1440, 900, dark: true);
        await CaptureEmptyTeamStateAsync(page, "mobile-dark", 390, 844, dark: true);
        await page.SetViewportSizeAsync(1440, 900);
        await page.EvaluateAsync("document.documentElement.setAttribute('data-theme', 'light')");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create a team" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/teams/manage$"));
        var createDialog = TeamE2E.CreateTeamDialog(page);
        await Expect(createDialog).ToBeVisibleAsync();
        await CaptureDialogAsync(createDialog, "issue-119-create-dialog.png");
        await page.ClickWhenInteractiveAsync(createDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }));
        await Expect(createDialog).Not.ToBeVisibleAsync();
        await Expect(page.GetByText("You do not have a team yet.")).ToBeVisibleAsync();

        using var api = App.CreateApiClient(captain);
        Assert.Empty((await TeamE2E.GetSummaryAsync(api)).CaptainedTeams);
    }

    private static async Task CaptureEmptyTeamStateAsync(IPage page, string name, int width, int height, bool dark)
    {
        await page.SetViewportSizeAsync(width, height);
        await page.EvaluateAsync($"document.documentElement.setAttribute('data-theme', '{(dark ? "dark" : "light")}')");
        var shotsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.orch/shots"));
        Directory.CreateDirectory(shotsDirectory);
        await page.Locator("div.registration-dialog").ScreenshotAsync(new() { Path = Path.Combine(shotsDirectory, $"issue-119-{name}.png") });
        var card = page.Locator(".registration-team-empty-state");
        var button = card.GetByRole(AriaRole.Button, new() { Name = "Create a team" });
        var cardBox = await card.BoundingBoxAsync();
        var buttonBox = await button.BoundingBoxAsync();
        Assert.NotNull(cardBox);
        Assert.NotNull(buttonBox);
        if (name.StartsWith("mobile", StringComparison.Ordinal))
            Assert.True(buttonBox.Width >= cardBox.Width * 0.8, "The mobile CTA should use the available card width.");
        else
            Assert.True(buttonBox.Width < cardBox.Width * 0.7, "The wide-screen CTA should stay content-width and centered.");
    }

    private static async Task CaptureDialogAsync(ILocator dialog, string filename)
    {
        var shotsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.orch/shots"));
        Directory.CreateDirectory(shotsDirectory);
        await dialog.ScreenshotAsync(new() { Path = Path.Combine(shotsDirectory, filename) });
    }

    [Fact]
    public async Task CaptainWithOnlyUndersizedTeamsGetsManagementGuidance()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-small-team-admin", isAdmin: true));
        var tournamentId = await CreateTeamTournamentAsync(adminApi, TournamentE2E.Unique("E2E Small Team"), teamSize: 2);
        var captain = await App.CreatePersonaAsync("reg-small-team-captain");
        var captainApi = App.CreateApiClient(captain);
        await TournamentE2E.CreateTeamAsync(captainApi, captain.UserId!.Value, TournamentE2E.Unique("E2E Small Squad"));
        var context = await App.NewAuthenticatedContextAsync(captain);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");

        var registration = page.Locator("#registration");
        await registration.GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog.GetByText("Teams with fewer than 2 members are hidden.")).ToBeVisibleAsync();
        var manageTeams = dialog.GetByRole(AriaRole.Button, new() { Name = "Manage teams" });
        await Expect(manageTeams).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Create a team" })).ToHaveCountAsync(0);
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Choose roster" })).ToHaveCountAsync(0);
        await CaptureDialogAsync(dialog, "issue-119-undersized-desktop.png");
        var dialogBox = await dialog.BoundingBoxAsync();
        var manageTeamsBox = await manageTeams.BoundingBoxAsync();
        Assert.NotNull(dialogBox);
        Assert.NotNull(manageTeamsBox);
        Assert.True(manageTeamsBox.Width < dialogBox.Width * 0.7, "The wide-screen Manage teams action should stay content-width.");
    }

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
