using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class TournamentPartnerAndSwissPlaywrightTests : TournamentE2ETestBase
{
    public TournamentPartnerAndSwissPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task AdminCanAssignAndClearTournamentPartnerSponsor()
    {
        var admin = await App.CreatePersonaAsync("partner-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var sponsorName = TournamentE2E.Unique("E2E Partner");
        await CreateSponsorAsync(adminApi, sponsorName, "Presenting", "https://example.test/partner", "Official partner");
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Partner Cup"));

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        // No partner assigned yet, so the featured partner card is absent.
        await Expect(page.Locator(".Tournament-detail-partner-card")).ToHaveCountAsync(0);

        await page.Locator("#tournament-sponsor").SelectOptionAsync(new SelectOptionValue { Label = sponsorName });
        await page.GetByRole(AriaRole.Button, new() { Name = "Save sponsor" }).ClickAsync();

        var partnerCard = page.Locator(".Tournament-detail-partner-card");
        await Expect(partnerCard).ToBeVisibleAsync();
        await Expect(partnerCard).ToContainTextAsync(sponsorName);

        await TournamentE2E.ReloadInteractiveAsync(page);
        await Expect(page.Locator(".Tournament-detail-partner-card")).ToContainTextAsync(sponsorName);

        // Clearing the selection removes the partner again.
        await page.Locator("#tournament-sponsor").SelectOptionAsync(new SelectOptionValue { Label = "No sponsor selected." });
        await page.GetByRole(AriaRole.Button, new() { Name = "Save sponsor" }).ClickAsync();
        await Expect(page.Locator(".Tournament-detail-partner-card")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SponsorAssignmentControlIsHiddenForNonAdmins()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("partner-nonadmin-admin", isAdmin: true));
        var id = await CreateIndividualTournamentAsync(adminApi, TournamentE2E.Unique("E2E Partner Hidden"));

        var context = await App.NewAuthenticatedContextAsync(await App.CreatePersonaAsync("partner-nonadmin-member"));
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await Expect(page.Locator("#tournament-sponsor-admin")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SwissTournamentCannotBeStartedAndShowsUnsupportedReason()
    {
        var admin = await App.CreatePersonaAsync("swiss-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateTournamentAsync(adminApi, TournamentE2E.Unique("E2E Swiss"), "Swiss", "Individual");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var start = page.Locator(".Tournament-admin-actions").GetByRole(AriaRole.Button, new() { Name = "Start" });
        await Expect(start).ToBeDisabledAsync();
        await Expect(page.Locator("#tournament-start-blocked")).ToHaveTextAsync("Swiss brackets are not supported yet.");
    }
}
