using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public class LeaderboardPlaywrightTests : TournamentE2ETestBase
{
    public LeaderboardPlaywrightTests(PlaywrightE2EFixture app) : base(app) { }

    [Fact]
    public async Task LeaderboardShowsEmptyStateAndBlocksEntryWhileScheduled()
    {
        var admin = await App.CreatePersonaAsync("board-scheduled-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Scheduled"), "HighestScore");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await Expect(page.GetByText(TournamentE2E.Text.LeaderboardNoResults)).ToBeVisibleAsync();

        var addEntry = page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult });
        await Expect(addEntry).ToBeDisabledAsync();
        await Expect(page.Locator("#leaderboard-entry-unavailable")).ToHaveTextAsync(TournamentE2E.Text.EntryUnavailableScheduled);
    }

    [Fact]
    public async Task AdminRecordsGuestAttemptThroughDialogAndStandingsPersist()
    {
        var admin = await App.CreatePersonaAsync("board-guest-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Guest"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");
        var guestName = TournamentE2E.Unique("Guest");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await RecordGuestAttemptThroughUiAsync(page, guestName, "120");

        var firstRow = page.Locator("table.leaderboard-table tbody tr").First;
        await Expect(firstRow).ToContainTextAsync(guestName);
        await Expect(firstRow.Locator(".leaderboard-rank")).ToHaveTextAsync("1");
        await Expect(firstRow).ToContainTextAsync("120");

        await TournamentE2E.ReloadInteractiveAsync(page);
        await Expect(page.Locator("table.leaderboard-table tbody tr").First).ToContainTextAsync(guestName);
    }

    [Fact]
    public async Task AdminRecordsExistingLanUserAttemptWithoutGuestBadge()
    {
        var admin = await App.CreatePersonaAsync("board-user-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var player = await App.CreatePersonaAsync("board-linked-player");
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board User"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult }).ClickAsync();

        var dialog = page.Locator("div.leaderboard-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Existing LAN user" }).ClickAsync();
        await dialog.Locator("#leaderboard-user-search").FillAsync(player.Username);
        await dialog.Locator(".leaderboard-user-results button").First.ClickAsync();
        await dialog.Locator("#leaderboard-attempt-value").FillAsync("88");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save result" }).ClickAsync();
        await Expect(dialog.Locator(".leaderboard-attempt-item").First).ToContainTextAsync("88");
        await CloseDialogAsync(dialog);

        var row = page.Locator("table.leaderboard-table tbody tr").First;
        await Expect(row).ToContainTextAsync(player.Username);
        await Expect(row.Locator(".leaderboard-kind-badge")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task FastestTimeLeaderboardRanksShortestDurationFirst()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("board-time-admin", isAdmin: true));
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Time"), "FastestTime");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");
        var slower = TournamentE2E.Unique("Slow");
        var faster = TournamentE2E.Unique("Fast");
        await TournamentE2E.RecordGuestAttemptAsync(adminApi, id, slower, durationMilliseconds: 45_000);
        await TournamentE2E.RecordGuestAttemptAsync(adminApi, id, faster, durationMilliseconds: 30_000);

        var context = await App.NewAuthenticatedContextAsync(await App.CreatePersonaAsync("board-time-viewer", isAdmin: true));
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var rows = page.Locator("table.leaderboard-table tbody tr");
        await Expect(rows.First).ToContainTextAsync(faster);
        await Expect(rows.First).ToContainTextAsync("0:30.000");
        await Expect(rows.Nth(1)).ToContainTextAsync(slower);

        // The dialog for a time-based leaderboard asks for whole milliseconds and previews the value.
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult }).ClickAsync();
        var dialog = page.Locator("div.leaderboard-dialog");
        await Expect(dialog).ToBeVisibleAsync();

        // Two participants already exist, so the dialog defaults to the existing-participant mode:
        // switch to the guest mode before the guest fields render.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "New guest" }).ClickAsync();
        await Expect(dialog.GetByLabel("Time (milliseconds)")).ToBeVisibleAsync();
        await dialog.Locator("#leaderboard-guest-name").FillAsync(TournamentE2E.Unique("Guest Time"));
        await dialog.Locator("#leaderboard-attempt-value").FillAsync("5000");
        // The duration preview is rendered from _valueInput, which the input binds on `onchange`.
        // Playwright's Fill only fires `input`, so commit the value the way a user does before
        // asserting on anything the component derives from it.
        await dialog.Locator("#leaderboard-attempt-value").PressAsync("Tab");
        await Expect(dialog.GetByText("Recorded as 0:05.000")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminCanEditAndRemoveAnAttemptWithConfirmation()
    {
        var admin = await App.CreatePersonaAsync("board-manage-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Manage"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");
        var guestName = TournamentE2E.Unique("Guest Manage");
        await TournamentE2E.RecordGuestAttemptAsync(adminApi, id, guestName, score: 100);

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult }).ClickAsync();

        var dialog = page.Locator("div.leaderboard-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        var existingParticipantMode = dialog.GetByRole(AriaRole.Button, new() { Name = "Existing participant" });
        if (await existingParticipantMode.IsEnabledAsync())
            await existingParticipantMode.ClickAsync();
        await dialog.Locator("#leaderboard-participant").SelectOptionAsync(new SelectOptionValue { Label = $"{guestName} (guest)" });

        var attempt = dialog.Locator(".leaderboard-attempt-item");
        await Expect(attempt).ToContainTextAsync("100");

        await attempt.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Edit }).ClickAsync();
        await dialog.Locator("[aria-label=\"Attempt value\"]").FillAsync("250");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save change" }).ClickAsync();
        await Expect(dialog.Locator(".leaderboard-attempt-item")).ToContainTextAsync("250");

        // Cancelling the destructive confirmation keeps the attempt.
        await dialog.Locator(".leaderboard-attempt-item").GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Delete }).ClickAsync();
        await dialog.Locator(".leaderboard-attempt-item").GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Cancel }).ClickAsync();
        await Expect(dialog.Locator(".leaderboard-attempt-item")).ToContainTextAsync("250");

        await dialog.Locator(".leaderboard-attempt-item").GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Delete }).ClickAsync();
        await dialog.Locator(".leaderboard-attempt-item").GetByRole(AriaRole.Button, new() { Name = "Remove attempt" }).ClickAsync();
        await Expect(dialog.GetByText("No attempts recorded yet.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NonAdminCannotSeeLeaderboardEntryControl()
    {
        var adminApi = App.CreateApiClient(await App.CreatePersonaAsync("board-nonadmin-admin", isAdmin: true));
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board NonAdmin"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");

        var context = await App.NewAuthenticatedContextAsync(await App.CreatePersonaAsync("board-nonadmin-member"));
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        await Expect(page.GetByText(TournamentE2E.Text.LeaderboardNoResults)).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task LeaderboardCannotBeFinishedUntilAResultExists()
    {
        var admin = await App.CreatePersonaAsync("board-finish-admin", isAdmin: true);
        var adminApi = App.CreateApiClient(admin);
        var id = await CreateLeaderboardTournamentAsync(adminApi, TournamentE2E.Unique("E2E Board Finish"), "HighestScore");
        await TournamentE2E.SetLifecycleAsync(adminApi, id, "InProgress");

        var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, $"/tournaments/{id}");

        var finish = page.Locator(".Tournament-admin-actions").GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.Finish });
        await Expect(finish).ToBeDisabledAsync();
        await Expect(page.Locator("#tournament-finish-blocked"))
            .ToHaveTextAsync(TournamentE2E.Text.LeaderboardResultsRequired);

        await RecordGuestAttemptThroughUiAsync(page, TournamentE2E.Unique("Guest Finish"), "10");

        await Expect(finish).ToBeEnabledAsync();
        await finish.ClickAsync();
        await Expect(page.Locator(".Tournament-status").First).ToHaveTextAsync(TournamentE2E.Text.StatusFinished);
    }

    private static async Task RecordGuestAttemptThroughUiAsync(IPage page, string guestName, string value)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = TournamentE2E.Text.RecordResult }).ClickAsync();
        var dialog = page.Locator("div.leaderboard-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.Locator("#leaderboard-guest-name").FillAsync(guestName);
        await dialog.Locator("#leaderboard-attempt-value").FillAsync(value);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save result" }).ClickAsync();
        await Expect(dialog.Locator(".leaderboard-attempt-item").First).ToBeVisibleAsync();
        await CloseDialogAsync(dialog);
    }

    private static async Task CloseDialogAsync(ILocator dialog)
    {
        await dialog.Locator(".leaderboard-dialog-footer")
            .GetByRole(AriaRole.Button, new() { Name = "Close" })
            .ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
    }
}
