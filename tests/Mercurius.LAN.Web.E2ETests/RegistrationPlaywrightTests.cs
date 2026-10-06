using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class RegistrationPlaywrightTests : TournamentE2ETestBase
{
    public RegistrationPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task IndividualRegistrationClosesConfirmationWithoutRegisteringWhenEligibilityRecheckFails()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-elig-admin", isAdmin: true));
        var member = await App.CreatePersonaAsync("reg-elig-member");
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Reg Eligibility"));

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var registration = page.Locator("#registration");
        await registration.GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        var confirmation = registration.Locator("[role=\"alertdialog\"]");
        await Expect(confirmation.GetByRole(AriaRole.Heading, new() { Name = TournamentE2E.Text.ConfirmRegistration }))
            .ToBeVisibleAsync();

        // Registering revalidates eligibility server-side; hiding the tournament table makes that
        // call fail, and the component answers by dropping the confirmation without registering.
        await using (await DatabaseReadFault.InstallAsync(App, "tournaments"))
        {
            await confirmation.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Register }).ClickAsync();
            await Expect(registration.Locator("[role=\"alertdialog\"]")).ToHaveCountAsync(0);
            await Expect(registration.GetByRole(AriaRole.Button, new() { Name = "Register now" })).ToBeVisibleAsync();
        }

        // With the database reachable again the same flow completes.
        await registration.GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        await registration.Locator("[role=\"alertdialog\"]")
            .GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Register })
            .ClickAsync();
        await Expect(registration.GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task AnonymousVisitorIsAskedToSignInOnOpenTournament()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-anon-admin", isAdmin: true));
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Reg Anon"));

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        // The anonymous prompt lives inside the registration dialog, so open it first.
        var registration = page.Locator("#registration");
        await registration.GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();
        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog).ToContainTextAsync("Sign in to register for this tournament.");
        await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = TournamentE2E.Text.SignInToRegister }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task MemberCanRegisterForIndividualTournamentAndRegistrationPersists()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-individual-admin", isAdmin: true));
        var member = await App.CreatePersonaAsync("reg-individual-member");
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Reg Individual"));

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var registration = page.Locator("#registration");
        await registration.GetByRole(AriaRole.Button, new() { Name = "Register now" }).ClickAsync();

        var confirmation = registration.Locator("[role=\"alertdialog\"]");
        await Expect(confirmation.GetByRole(AriaRole.Heading, new() { Name = TournamentE2E.Text.ConfirmRegistration }))
            .ToBeVisibleAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Register }).ClickAsync();

        await Expect(registration.GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();
        await Expect(page.Locator(".participants-overview-grid")).ToContainTextAsync(member.Username);

        await TournamentE2E.ReloadInteractiveAsync(page);
        await Expect(page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task MemberCanCancelIndividualRegistrationThroughConfirmation()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-cancel-admin", isAdmin: true));
        var member = await App.CreatePersonaAsync("reg-cancel-member");
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Reg Cancel"));
        await TournamentE2E.RegisterIndividualAsync(App.CreateApiClient(member), id);

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Manage registration" }).ClickAsync();
        var dialog = page.Locator("div.registration-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Your individual registration is active.")).ToBeVisibleAsync();

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel registration" }).ClickAsync();

        var unregisterConfirmation = dialog.Locator("[role=\"alertdialog\"]");
        await Expect(unregisterConfirmation.GetByRole(AriaRole.Heading, new() { Name = "Cancel individual registration" }))
            .ToBeVisibleAsync();
        await unregisterConfirmation
            .GetByRole(AriaRole.Button, new() { Name = "Cancel registration" })
            .ClickAsync();

        // The delete and the current-user refresh run server-side after the click returns, so wait
        // for the refreshed registration panel; reloading earlier reads pre-cancel state.
        await Expect(page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();

        await TournamentE2E.ReloadInteractiveAsync(page);
        // The cancellation persisted: the participant is gone and the panel is back to an entry state.
        await Expect(page.Locator(".participants-state-card")).ToContainTextAsync("No participants yet");
        await Expect(page.Locator("#registration").GetByRole(AriaRole.Button, new() { Name = "Register now" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task RegistrationIsClosedForNonScheduledTournament()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-closed-admin", isAdmin: true));
        var member = await App.CreatePersonaAsync("reg-closed-member");
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Reg Closed"));
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "Canceled");

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var closedBadge = page.Locator("#registration .registration-closed-badge");
        await Expect(closedBadge).ToBeVisibleAsync();
        await Expect(closedBadge).ToContainTextAsync(TournamentE2E.Text.RegistrationClosed);
    }

    [Fact]
    public async Task LeaderboardTournamentHasNoRegistrationPanel()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("reg-board-admin", isAdmin: true));
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Reg Board"), "HighestScore");

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await Expect(page.GetByText("No registration required").First).ToBeVisibleAsync();
        await Expect(page.Locator("#registration")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AdminCanRemoveIndividualRegistrationFromAdministrationPanel()
    {
        var admin = await App.CreatePersonaAsync("reg-admin-remove", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var member = await App.CreatePersonaAsync("reg-admin-remove-member");
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Reg Admin"));
        await TournamentE2E.RegisterIndividualAsync(App.CreateApiClient(member), id);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var adminPanel = page.Locator("#admin-registration");
        await Expect(adminPanel).ToBeVisibleAsync();
        await Expect(adminPanel.Locator(".admin-registration-row")).ToHaveCountAsync(1);
        await Expect(adminPanel).ToContainTextAsync(member.Username);

        await page.Locator("#admin-removal-reason").FillAsync("Removed by the E2E suite");
        await adminPanel.GetByRole(AriaRole.Button, new() { Name = "Remove" }).ClickAsync();

        await Expect(adminPanel.GetByText("No registrations are available to administer.")).ToBeVisibleAsync();
    }

}
