using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class TournamentOverviewPlaywrightTests : TournamentE2ETestBase
{
    public TournamentOverviewPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task AnonymousVisitorSeesTournamentCardWithStatusAndNoAdminControls()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("overview-anon-admin", isAdmin: true));
        var name = TournamentE2E.Unique("E2E Overview");
        await CreateIndividualTournamentAsync(adminApi, name);

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");
        await TournamentE2E.EnsureCardVisibleAsync(page, name);

        var card = Card(page, name);
        await Expect(card).ToBeVisibleAsync();
        await Expect(card.Locator(".tournament-status")).ToHaveTextAsync(TournamentE2E.Text.StatusOpen);
        await Expect(page.Locator("button.tournaments-add-card")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task StatusFilterChipHidesNonMatchingStatusesAndExposesPressedState()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("overview-status-admin", isAdmin: true));
        var prefix = TournamentE2E.Unique("E2E Status");
        var scheduledName = $"{prefix} scheduled";
        var cancelledName = $"{prefix} cancelled";
        await CreateIndividualTournamentAsync(adminApi, scheduledName);
        var cancelledId = await CreateIndividualTournamentAsync(adminApi, cancelledName);
        await TournamentE2E.SetLifecycleAsync(adminApi, cancelledId, "Canceled");

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");
        await TournamentE2E.EnsureCardVisibleAsync(page, scheduledName);

        await page.GetByLabel(TournamentE2E.Text.SearchTournaments).FillAsync(prefix);
        await Expect(Card(page, scheduledName)).ToBeVisibleAsync();
        await Expect(Card(page, cancelledName)).ToBeVisibleAsync();

        var cancelledChip = StatusChip(page, TournamentE2E.Text.StatusCancelled);
        await cancelledChip.ClickAsync();

        await Expect(cancelledChip).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Card(page, cancelledName)).ToBeVisibleAsync();
        await Expect(Card(page, scheduledName)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ParticipationFilterSeparatesSoloAndTeamTournaments()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("overview-participation-admin", isAdmin: true));
        var prefix = TournamentE2E.Unique("E2E Participation");
        var soloName = $"{prefix} solo";
        var teamName = $"{prefix} team";
        await CreateIndividualTournamentAsync(adminApi, soloName);
        await CreateTeamTournamentAsync(adminApi, teamName, teamSize: 2);

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");
        await TournamentE2E.EnsureCardVisibleAsync(page, soloName);

        await page.GetByLabel(TournamentE2E.Text.SearchTournaments).FillAsync(prefix);
        await Expect(Card(page, soloName)).ToBeVisibleAsync();
        await Expect(Card(page, teamName)).ToBeVisibleAsync();

        await ParticipationChip(page, "Solo").ClickAsync();
        await Expect(Card(page, soloName)).ToBeVisibleAsync();
        await Expect(Card(page, teamName)).ToHaveCountAsync(0);

        await ParticipationChip(page, "Team").ClickAsync();
        await Expect(Card(page, teamName)).ToBeVisibleAsync();
        await Expect(Card(page, soloName)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SearchWithoutMatchesShowsEmptyState()
    {
        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        await page.GetByLabel(TournamentE2E.Text.SearchTournaments)
            .FillAsync("zz-no-such-tournament-" + Guid.NewGuid().ToString("N"));

        await Expect(page.GetByText(TournamentE2E.Text.NoFilterMatches)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SortingByStatusPlacesScheduledTournamentFirst()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("overview-sort-admin", isAdmin: true));
        var prefix = TournamentE2E.Unique("E2E Sort");
        var cancelledId = await CreateIndividualTournamentAsync(adminApi, $"{prefix} one");
        await TournamentE2E.SetLifecycleAsync(adminApi, cancelledId, "Canceled");
        await CreateIndividualTournamentAsync(adminApi, $"{prefix} two");

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");
        await TournamentE2E.EnsureCardVisibleAsync(page, $"{prefix} two");

        await page.GetByLabel(TournamentE2E.Text.SearchTournaments).FillAsync(prefix);
        await page.GetByLabel(TournamentE2E.Text.SortTournaments).SelectOptionAsync("Status");

        await Expect(page.Locator("article.tournaments-card h3").First).ToContainTextAsync("two");
    }

    [Fact]
    public async Task NonScheduledTournamentCardClosesRegistration()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("overview-closed-admin", isAdmin: true));
        var name = TournamentE2E.Unique("E2E Closed");
        var id = await CreateIndividualTournamentAsync(adminApi, name);
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "Canceled");

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");
        await TournamentE2E.EnsureCardVisibleAsync(page, name);
        await page.GetByLabel(TournamentE2E.Text.SearchTournaments).FillAsync(name);

        var closedButton = Card(page, name)
            .GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RegistrationClosed });
        await Expect(closedButton).ToBeDisabledAsync();
    }

    [Fact]
    public async Task AdminSeesAddTournamentEntryPoint()
    {
        var admin = await App.CreatePersonaAsync("overview-admin-card", isAdmin: true);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        await Expect(page.Locator("button.tournaments-add-card")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PaginationShowsNextPageWhenMoreThanOnePageOfTournamentsExists()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("overview-paging-admin", isAdmin: true));
        var prefix = TournamentE2E.Unique("E2E Page");

        for (var i = 0; i < 25; i++)
            await CreateIndividualTournamentAsync(adminApi, $"{prefix} {i:D2}");

        var context = await App.NewContextAsync();
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        var pagination = page.Locator("nav.tournaments-pagination");
        await Expect(pagination).ToBeVisibleAsync();
        await Expect(pagination.GetByRole(AriaRole.Button, new() { Name = "Next" })).ToBeEnabledAsync();

        await pagination.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();

        await Expect(pagination.Locator("[aria-current=\"page\"]")).ToHaveTextAsync("Page 2");
        await Expect(pagination.GetByRole(AriaRole.Button, new() { Name = "Back" })).ToBeEnabledAsync();
    }

    private static ILocator Card(IPage page, string name) =>
        page.Locator("article.tournaments-card").Filter(new() { HasText = name });

    private static ILocator StatusChip(IPage page, string label) =>
        page.Locator(".tournaments-filter-group").Nth(0)
            .Locator("button.tournaments-filter-chip", new() { HasText = label });

    private static ILocator ParticipationChip(IPage page, string label) =>
        page.Locator(".tournaments-filter-group").Nth(1)
            .Locator("button.tournaments-filter-chip", new() { HasText = label });
}
