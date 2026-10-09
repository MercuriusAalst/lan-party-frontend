using System.Net;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class SponsorManagementPlaywrightTests : TournamentE2ETestBase
{
    public SponsorManagementPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    private const string Forbidden = "Forbidden";

    [Fact]
    public async Task AdminCanCreateSponsorAndSeeItInTheManagementList()
    {
        var admin = await App.CreatePersonaAsync("sponsor-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Sponsor");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/admin/sponsors");

        await page.Locator("#name").FillAsync(name);
        await page.Locator("#infoUrl").FillAsync("https://example.test/e2e");
        await page.Locator("#sponsorTier").SelectOptionAsync("Gold");
        await page.Locator("#description").FillAsync("Created by the E2E suite");
        await page.Locator("#sponsorLogo").SetInputFilesAsync(TournamentE2E.TestImagePath());
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateSponsor }).ClickAsync();

        await Expect(page.GetByText("1 sponsor currently listed.")).ToBeVisibleAsync();

        // Reload proves the sponsor persisted server-side; the admin search finds it again.
        await TournamentE2E.ReloadInteractiveAsync(page);
        await page.WaitForInteractiveAsync();
        await page.Locator(".custom-autocomplete-input").FillAsync(name);
        await Expect(page.Locator(".custom-autocomplete-dropdown li").Filter(new() { HasText = name }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminCanEditSponsorAndChangePersists()
    {
        var admin = await App.CreatePersonaAsync("sponsor-editor", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var original = TournamentE2E.Unique("E2E Sponsor Edit");
        var updated = TournamentE2E.Unique("E2E Sponsor Renamed");
        await CreateSponsorAsync(adminApi, original, "Silver", "https://example.test/before");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/admin/sponsors");

        await SelectSponsorAsync(page, original);
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.UpdateSponsor })).ToBeVisibleAsync();

        await page.Locator("#name").FillAsync(updated);
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.UpdateSponsor }).ClickAsync();
        await Expect(page.GetByText($"Editing {updated}.")).ToBeVisibleAsync();

        await TournamentE2E.ReloadInteractiveAsync(page);
        await page.Locator(".custom-autocomplete-input").FillAsync(updated);
        await Expect(page.Locator(".custom-autocomplete-dropdown li").Filter(new() { HasText = updated }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminCanDeleteSponsorAndItDisappearsFromTheList()
    {
        var admin = await App.CreatePersonaAsync("sponsor-deleter", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Sponsor Delete");
        await CreateSponsorAsync(adminApi, name, "Bronze", "https://example.test/delete");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/admin/sponsors");

        await SelectSponsorAsync(page, name);
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.DeleteSponsor }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateSponsor })).ToBeVisibleAsync();
        await page.Locator(".custom-autocomplete-input").FillAsync(name);
        await Expect(page.Locator(".custom-autocomplete-dropdown li").Filter(new() { HasText = name }))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ClearingFormReturnsToCreateModeWithoutSaving()
    {
        var admin = await App.CreatePersonaAsync("sponsor-clear", isAdmin: true);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/admin/sponsors");

        await page.Locator("#name").FillAsync("Draft sponsor that should be discarded");
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Clear }).ClickAsync();

        await Expect(page.Locator("#name")).ToHaveValueAsync(string.Empty);
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateSponsor })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SubmittingSponsorFormWithMissingFieldsShowsValidation()
    {
        var admin = await App.CreatePersonaAsync("sponsor-invalid", isAdmin: true);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/admin/sponsors");

        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateSponsor }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateSponsor })).ToBeVisibleAsync();
        await Expect(page.Locator(".validation-message").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NonAdminCannotOpenSponsorManagement()
    {
        var member = await App.CreatePersonaAsync("sponsor-nonadmin");

        var context = await App.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        var response = await page.GotoAsync("/admin/sponsors");

        // A signed-in non-admin hits the SSR authorization boundary: the endpoint answers 403 and the
        // status-code middleware re-executes the Forbidden page. The address bar keeps the original
        // URL, so assert the status code and rendered state instead of a redirect.
        Assert.Equal((int)HttpStatusCode.Forbidden, response!.Status);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = Forbidden })).ToBeVisibleAsync();
        await Expect(page.GetByText(TournamentE2E.Text.AccessDenied)).ToBeVisibleAsync();
        await Expect(page.Locator(".sponsor-admin-page")).ToHaveCountAsync(0);

        // A forbidden page that only looks right in SSR can still be a dead circuit; prove the shell
        // still responds and the browser saw no fatal render message.
        await page.AssertShippedShellStaysInteractiveAsync();
    }

    [Fact]
    public async Task AnonymousVisitorIsChallengedWhenOpeningSponsorManagement()
    {
        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();

        // The SSR redirect leaves the Blazor app entirely, so wait on the IdP page instead.
        await page.GotoAsync("/admin/sponsors");

        await Expect(page.GetByText("Mercurius E2E sign in")).ToBeVisibleAsync();
    }

    private static async Task SelectSponsorAsync(IPage page, string sponsorName)
    {
        var search = page.Locator(".custom-autocomplete-input");
        await search.ClickAsync();
        await search.FillAsync(sponsorName);
        await page.Locator(".custom-autocomplete-dropdown li").Filter(new() { HasText = sponsorName }).ClickAsync();
    }
}
