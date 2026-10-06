using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class LeaderboardValidationPlaywrightTests : TournamentE2ETestBase
{
    public LeaderboardValidationPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task GuestEntryRequiresDisplayNameAndResultValue()
    {
        var admin = await App.CreatePersonaAsync("board-validation-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Validation"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult }).ClickAsync();

        var dialog = page.Locator("div.leaderboard-dialog");
        await Expect(dialog).ToBeVisibleAsync();

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save result" }).ClickAsync();
        await Expect(dialog.Locator(".leaderboard-dialog-error")).ToHaveTextAsync("Enter a guest display name.");

        await dialog.Locator("#leaderboard-guest-name").FillAsync(TournamentE2E.Unique("Guest Valid"));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save result" }).ClickAsync();
        await Expect(dialog.Locator(".leaderboard-dialog-error")).ToHaveTextAsync("Enter a result value.");
    }

    [Fact]
    public async Task ScoreEntryRejectsNegativeValues()
    {
        var admin = await App.CreatePersonaAsync("board-negative-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Negative"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult }).ClickAsync();

        var dialog = page.Locator("div.leaderboard-dialog");
        await dialog.Locator("#leaderboard-guest-name").FillAsync(TournamentE2E.Unique("Guest Negative"));
        await dialog.Locator("#leaderboard-attempt-value").FillAsync("-5");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save result" }).ClickAsync();

        await Expect(dialog.Locator(".leaderboard-dialog-error"))
            .ToHaveTextAsync("Enter a non-negative score with at most twelve digits before and six after the decimal point.");
    }

    [Fact]
    public async Task LinkedUserModeRequiresSelectionAndReportsNoMatches()
    {
        var admin = await App.CreatePersonaAsync("board-search-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Search"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult }).ClickAsync();

        var dialog = page.Locator("div.leaderboard-dialog");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Existing LAN user" }).ClickAsync();

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save result" }).ClickAsync();
        await Expect(dialog.Locator(".leaderboard-dialog-error")).ToHaveTextAsync("Select a LAN user.");

        await dialog.Locator("#leaderboard-user-search").FillAsync("zz" + Guid.NewGuid().ToString("N")[..6]);
        await Expect(dialog.GetByText("No matching LAN users.")).ToBeVisibleAsync();
    }
}
