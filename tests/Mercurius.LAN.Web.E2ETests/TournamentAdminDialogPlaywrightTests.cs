using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class TournamentAdminDialogPlaywrightTests : TournamentE2ETestBase
{
    public TournamentAdminDialogPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task EditingScheduledTournamentKeepsTheChangedStartAndSavedContactAdmin()
    {
        var firstAdmin = await App.CreatePersonaAsync("aaa-admin", isAdmin: true);
        var assignedAdmin = await App.CreatePersonaAsync("zzz-admin", isAdmin: true);
        var firstAdminApi = App.CreateApiClient(firstAdmin);
        var tournamentId = await TournamentE2E.CreateTournamentAsync(
            firstAdminApi,
            TournamentE2E.Unique("E2E Edit fields"),
            "SingleElimination",
            "Individual",
            assignedAdminUserId: assignedAdmin.UserId);
        var created = await TournamentE2E.ReadJsonAsync(await firstAdminApi.GetAsync($"v1/lan/tournaments/{tournamentId}"));
        Assert.Equal(assignedAdmin.UserId, created.GetProperty("contactAdmin").GetProperty("id").GetGuid());

        var context = await App.NewAuthenticatedContextAsync(firstAdmin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();

        var contactAdmin = page.Locator("#contactAdmin");
        await Expect(contactAdmin).ToHaveAttributeAsync("aria-busy", "false");
        await Expect(contactAdmin).ToHaveValueAsync(assignedAdmin.UserId!.Value.ToString());
        Assert.NotEqual(
            await contactAdmin.Locator("option").Nth(1).GetAttributeAsync("value"),
            assignedAdmin.UserId.Value.ToString());

        var startTime = page.Locator("#plannedStartTime");
        await startTime.FillAsync("2030-05-06T18:15");
        await startTime.PressAsync("Tab");
        await Expect(startTime).ToHaveValueAsync("2030-05-06T18:15");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Expect(page.Locator("#plannedStartTime")).ToHaveCountAsync(0);

        var saved = await TournamentE2E.ReadJsonAsync(await firstAdminApi.GetAsync($"v1/lan/tournaments/{tournamentId}"));
        Assert.Equal(new DateTime(2030, 5, 6), DateTimeOffset.Parse(saved.GetProperty("plannedStartTime").GetString()!).UtcDateTime.Date);
        Assert.Equal(assignedAdmin.UserId, saved.GetProperty("contactAdmin").GetProperty("id").GetGuid());

        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{tournamentId}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await Expect(page.Locator("#contactAdmin")).ToHaveAttributeAsync("aria-busy", "false");
        await Expect(page.Locator("#plannedStartTime")).ToHaveValueAsync("2030-05-06T18:15");
        await Expect(page.Locator("#contactAdmin")).ToHaveValueAsync(assignedAdmin.UserId.Value.ToString());
    }

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
        await SelectAssignedAdminAsync(dialog, admin);
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
        await SelectAssignedAdminAsync(dialog, admin);
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
        // The assigned admin is mandatory: the picker offers no "none" choice.
        await Expect(dialog.Locator("#contactAdmin option[value=\"\"]")).ToBeDisabledAsync();
    }

    [Fact]
    public async Task AssignedAdminIsRequiredEvenWhenEveryOtherFieldIsValid()
    {
        var admin = await App.CreatePersonaAsync("dialog-noadmin-admin", isAdmin: true);
        var name = TournamentE2E.Unique("E2E Dialog NoAdmin");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/tournaments");

        await OpenDialogAsync(page);
        var dialog = page.Locator("[role=\"dialog\"]");
        await dialog.Locator("#name").FillAsync(name);
        await dialog.Locator("#tournamentImage").SetInputFilesAsync(TournamentE2E.TestImagePath());
        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();

        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Select an assigned admin.")).ToBeVisibleAsync();

        await SelectAssignedAdminAsync(dialog, admin);
        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();
        await Expect(page.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);
        await Expect(page.Locator("article.tournaments-card").Filter(new() { HasText = name })).ToBeVisibleAsync();
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
        await SelectAssignedAdminAsync(dialog, admin);

        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Team tournaments require a team size between 1 and 50.")).ToBeVisibleAsync();

        await dialog.Locator("#teamSize").FillAsync("2");
        await dialog.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.CreateTournament }).ClickAsync();

        await Expect(page.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);
        await Expect(page.Locator("article.tournaments-card").Filter(new() { HasText = name })).ToBeVisibleAsync();
    }

    private static async Task SelectAssignedAdminAsync(ILocator dialog, E2EPersona admin) =>
        await dialog.Locator("#contactAdmin").SelectOptionAsync(admin.UserId!.Value.ToString());

    private static async Task OpenDialogAsync(IPage page)
    {
        await page.Locator("button.tournaments-add-card").ClickAsync();
        await Expect(page.Locator("[role=\"dialog\"]")).ToBeVisibleAsync();
    }
}
