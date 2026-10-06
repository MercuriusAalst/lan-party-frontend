using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class TournamentAdminDialogPlaywrightTests : TournamentE2ETestBase
{
    public TournamentAdminDialogPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task AdminCanCreateIndividualTournamentThroughDialog()
    {
        var admin = await App.CreatePersonaAsync("dialog-create-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Dialog");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        await OpenDialogAsync(page);
        var dialog = page.Locator("[role=\"dialog\"]");
        await dialog.Locator("#name").FillAsync(name);
        await dialog.Locator("#tournamentImage").SetInputFilesAsync(TournamentE2E.TestImagePath());
        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();

        await Expect(page.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);
        var card = page.Locator("article.tournaments-card").Filter(new() { HasText = name });
        await Expect(card).ToBeVisibleAsync();

        // Prove the tournament really persisted by opening its detail page after a fresh navigation.
        var id = await TournamentE2E.FindTournamentIdAsync(adminApi, name);
        Assert.NotNull(id);
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SelectingLeaderboardBracketSwapsParticipationForRankingMetric()
    {
        var admin = await App.CreatePersonaAsync("dialog-leaderboard-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Dialog Board");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        await OpenDialogAsync(page);
        var dialog = page.Locator("[role=\"dialog\"]");
        await dialog.Locator("#bracketType").SelectOptionAsync("Leaderboard");

        await Expect(dialog.Locator("#leaderboardRankingMetric")).ToBeVisibleAsync();
        await Expect(dialog.Locator("#participationMode")).ToHaveCountAsync(0);

        await dialog.Locator("#name").FillAsync(name);
        await dialog.Locator("#leaderboardRankingMetric").SelectOptionAsync("HighestScore");
        await dialog.Locator("#tournamentImage").SetInputFilesAsync(TournamentE2E.TestImagePath());
        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();

        await Expect(page.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);
        var card = page.Locator("article.tournaments-card").Filter(new() { HasText = name });
        await Expect(card).ToBeVisibleAsync();
        await Expect(card.GetByText("Leaderboard", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CreatingTournamentWithoutRequiredFieldsKeepsDialogAndShowsValidation()
    {
        var admin = await App.CreatePersonaAsync("dialog-invalid-admin", isAdmin: true);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        await OpenDialogAsync(page);
        var dialog = page.Locator("[role=\"dialog\"]");
        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();

        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.Locator(".validation-message").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TeamTournamentRequiresTeamSizeBeforeCreating()
    {
        var admin = await App.CreatePersonaAsync("dialog-team-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var name = TournamentE2E.Unique("E2E Dialog Team");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        await OpenDialogAsync(page);
        var dialog = page.Locator("[role=\"dialog\"]");
        await dialog.Locator("#name").FillAsync(name);
        await dialog.Locator("#participationMode").SelectOptionAsync("Team");
        await Expect(dialog.Locator("#teamSize")).ToBeVisibleAsync();
        await dialog.Locator("#tournamentImage").SetInputFilesAsync(TournamentE2E.TestImagePath());

        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Team tournaments require a team size between 1 and 50.")).ToBeVisibleAsync();

        await dialog.Locator("#teamSize").FillAsync("2");
        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();

        await Expect(page.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);
        await Expect(page.Locator("article.tournaments-card").Filter(new() { HasText = name })).ToBeVisibleAsync();
    }

    private static async Task OpenDialogAsync(IPage page)
    {
        await page.Locator("button.tournaments-add-card").ClickAsync();
        await Expect(page.Locator("[role=\"dialog\"]")).ToBeVisibleAsync();
    }
}
