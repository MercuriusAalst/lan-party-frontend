using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class TournamentDetailLifecyclePlaywrightTests : TournamentE2ETestBase
{
    public TournamentDetailLifecyclePlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task TournamentDetailLoadFailureShowsRetryAndRecoversInPlace()
    {
        var admin = await App.CreatePersonaAsync("detail-fault-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Detail Fault");
        var id = await CreateIndividualTournamentAsync(adminApi, name);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();

        await using (await DatabaseReadFault.InstallAsync(App, "tournaments"))
        {
            await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");
            // Scope to the error section that carries the retry control: Blazored toasts are also
            // rendered into the page and would make a bare text/role locator ambiguous.
            await Expect(LoadErrorSection(page)).ToContainTextAsync(TournamentE2E.Text.LoadError);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.TryAgain })).ToBeVisibleAsync();
        }

        // The fault is restored, so the same component must recover without a full page reload.
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.TryAgain }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
        await Expect(LoadErrorSection(page)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task UnknownTournamentIdShowsNotFoundState()
    {
        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();

        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{Guid.NewGuid()}");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = TournamentE2E.Text.NotFoundTitle }))
            .ToBeVisibleAsync();
        await Expect(page.GetByText(TournamentE2E.Text.NotFoundText)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task StartIsBlockedUntilTwoActiveRegistrationsExistThenStartsTournament()
    {
        var admin = await App.CreatePersonaAsync("detail-start-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Start");
        var id = await CreateIndividualTournamentAsync(adminApi, name);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var adminActions = page.Locator(".Tournament-admin-actions");
        var start = adminActions.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Start });
        await Expect(start).ToBeDisabledAsync();
        await Expect(page.Locator("#tournament-start-blocked"))
            .ToHaveTextAsync(TournamentE2E.Text.StartRequiresRegistrations);

        await TournamentE2E.RegisterIndividualAsync(
            App.CreateApiClient(await App.CreatePersonaAsync("detail-start-first")), id);
        await TournamentE2E.RegisterIndividualAsync(
            App.CreateApiClient(await App.CreatePersonaAsync("detail-start-second")), id);

        await TournamentE2E.ReloadInteractiveAsync(page);
        await Expect(start).ToBeEnabledAsync();
        await start.ClickAsync();

        await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync(TournamentE2E.Text.StatusOngoing);
    }

    [Fact]
    public async Task AdminCanCancelScheduledAndResetCanceledTournament()
    {
        var admin = await App.CreatePersonaAsync("detail-cancel-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Cancel"));

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var adminActions = page.Locator(".Tournament-admin-actions");
        await adminActions.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Cancel }).ClickAsync();
        await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync(TournamentE2E.Text.StatusCancelled);

        await adminActions.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Reset }).ClickAsync();
        await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync(TournamentE2E.Text.StatusOpen);
    }

    [Fact]
    public async Task InProgressTournamentCannotBeDeletedAndFinishCompletesIt()
    {
        var admin = await App.CreatePersonaAsync("detail-finish-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Finish"));
        var first = await App.CreatePersonaAsync("detail-finish-first");
        var second = await App.CreatePersonaAsync("detail-finish-second");
        await TournamentE2E.RegisterIndividualAsync(App.CreateApiClient(first), id);
        await TournamentE2E.RegisterIndividualAsync(App.CreateApiClient(second), id);
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");

        // Finishing assigns placements from the bracket, so complete the final through both participants first.
        await MatchE2E.CompleteMatchViaApiAsync(App, (await MatchE2E.GetMatchesAsync(adminApi, id)).Single().Id,
            first, second, 1, 0);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var adminActions = page.Locator(".Tournament-admin-actions");
        await Expect(adminActions.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Delete }))
            .ToBeDisabledAsync();
        await Expect(page.Locator("#tournament-delete-blocked")).ToHaveTextAsync(TournamentE2E.Text.DeleteInProgress);

        await page.ClickWhenInteractiveAsync(
            adminActions.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Finish }));
        await ExpectWithToastDiagnosticsAsync(page, TournamentE2E.Text.StatusFinished);
    }

    [Fact]
    public async Task AdminCanDeleteScheduledTournamentAndReturnsToOverview()
    {
        var admin = await App.CreatePersonaAsync("detail-delete-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Delete");
        var id = await CreateIndividualTournamentAsync(adminApi, name);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await page.Locator(".Tournament-admin-actions")
            .GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Delete })
            .ClickAsync();

        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/tournaments$"));
        await page.GetByLabel(TournamentE2E.Text.SearchTournaments).FillAsync(name);
        await Expect(page.GetByText(TournamentE2E.Text.NoFilterMatches)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NonAdminSeesTournamentWithoutLifecycleControls()
    {
        var admin = await App.CreatePersonaAsync("detail-nonadmin-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E NonAdmin");
        var id = await CreateIndividualTournamentAsync(adminApi, name);
        var member = await App.CreatePersonaAsync("detail-nonadmin-member");

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
        await Expect(page.Locator(".Tournament-admin-actions")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AdminCanEditScheduledTournamentAndChangePersists()
    {
        var admin = await App.CreatePersonaAsync("detail-edit-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Edit");
        var updated = $"{name} v2";
        var id = await CreateIndividualTournamentAsync(adminApi, name);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Edit, Exact = true }).ClickAsync();
        var nameInput = page.Locator("#name");
        await Expect(nameInput).ToHaveValueAsync(name);
        await nameInput.FillAsync(updated);
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Save }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = updated })).ToBeVisibleAsync();

        await TournamentE2E.ReloadInteractiveAsync(page);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = updated })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task EditControlIsHiddenOnceTournamentIsNoLongerScheduled()
    {
        var admin = await App.CreatePersonaAsync("detail-noedit-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E NoEdit"));
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "Canceled");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync(TournamentE2E.Text.StatusCancelled);
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Edit, Exact = true }))
            .ToHaveCountAsync(0);
    }

    private static ILocator LoadErrorSection(IPage page) =>
        page.Locator("section.brand-section")
            .Filter(new() { Has = page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.TryAgain }) });

    /// <summary>
    /// Asserts the hero status badge, and on failure appends the rendered toast text so the TRX shows
    /// the real API message instead of only "expected Finished".
    /// </summary>
    private static async Task ExpectWithToastDiagnosticsAsync(IPage page, string expectedStatus)
    {
        try
        {
            await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync(expectedStatus);
        }
        catch (PlaywrightException exception)
        {
            var toasts = await page.Locator(".blazored-toast-message").AllTextContentsAsync();
            throw new Xunit.Sdk.XunitException(
                $"{exception.Message}{Environment.NewLine}Rendered toasts: {string.Join(" | ", toasts)}");
        }
    }
}
