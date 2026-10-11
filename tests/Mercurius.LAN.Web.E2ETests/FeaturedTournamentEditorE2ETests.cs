using System.Net;
using System.Net.Http;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The administrator's inline featured-tournament editor on the homepage, driven in a real browser
/// against the real API. Covers the edit entry point and its role guard, the actual HTML5 drag
/// between the lead and secondary cards, the draft-only guarantee (the hero and the public page stay
/// on the saved order until a save is confirmed), the save/cancel lifecycle, the add/remove and
/// compact position controls, and the editing layout at desktop and intermediate viewports.
///
/// A persisted order is always proven by an independent anonymous navigation, so a client-side-only
/// "save" cannot pass this class.
/// </summary>
[Collection(E2ECollection.Name)]
public class FeaturedTournamentEditorE2ETests(PlaywrightE2EFixture app) : TournamentE2ETestBase(app)
{
    [Fact]
    public async Task AdminDragsASecondaryCardIntoTheLeadAndSavePersistsOnThePublicHomepage()
    {
        var admin = await App.CreatePersonaAsync("featured-editor-admin", isAdmin: true);
        using var adminApi = App.CreateApiClient(admin);
        var names = await SeedFeaturedAsync(adminApi);

        await using var adminContext = await App.NewAuthenticatedContextAsync(admin);
        var page = await adminContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/");
        await Expect(PublicLead(page)).ToContainTextAsync(names[0]);

        // Independent public baseline: the page state an anonymous visitor sees right now.
        await using var publicContext = await App.NewContextAsync();
        var publicPage = await publicContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(publicPage, "/");
        await Expect(PublicLead(publicPage)).ToContainTextAsync(names[0]);

        await OpenEditorAsync(page);
        var heroPrimary = page.Locator(".home-hero-media-image--primary");
        var heroSourceBeforeDraft = await heroPrimary.GetAttributeAsync("src");

        // Drag the second card (position 2) onto the lead slot (position 1). The gesture starts on
        // the card's grip and drops on the lead's top-left: a drop on the card centre sends the
        // pointer across the slot2 position <select>, which then reorders through that control
        // instead of through the drop.
        var leadSlot = EditorLead(page);
        var sourceRow = EditorRow(page, 0);
        await Expect(sourceRow).ToContainTextAsync(names[1]);
        await sourceRow.Locator(".home-tournament-slot-grip").DragToAsync(leadSlot,
            new() { TargetPosition = new TargetPosition { X = 24, Y = 24 } });
        await Expect(leadSlot).ToContainTextAsync(names[1]);

        // Draft only: neither the hero nor the public page may follow the unsaved arrangement.
        Assert.Equal(heroSourceBeforeDraft, await heroPrimary.GetAttributeAsync("src"));
        await TournamentE2E.ReloadInteractiveAsync(publicPage);
        await Expect(PublicLead(publicPage)).ToContainTextAsync(names[0]);

        await SaveAsync(page);

        // The saved arrangement is now the published one.
        await Expect(PublicLead(page)).ToContainTextAsync(names[1]);
        await Expect(PublicRow(page, 0)).ToContainTextAsync(names[0]);
        await Expect(PublicRow(page, 1)).ToContainTextAsync(names[2]);

        // The hero follows the saved lead and reuses the same lead-card asset.
        var leadImageSource = await page
            .Locator(".home-tournament-lead img.home-tournament-image--lead")
            .GetAttributeAsync("src");
        Assert.NotNull(leadImageSource);
        await Expect(heroPrimary).ToHaveAttributeAsync("src", leadImageSource!);
        Assert.NotEqual(heroSourceBeforeDraft, leadImageSource);

        // Server persistence: a fresh anonymous navigation reads the new arrangement from the API.
        await using var verifyContext = await App.NewContextAsync();
        var verifyPage = await verifyContext.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(verifyPage, "/");
        await Expect(PublicLead(verifyPage)).ToContainTextAsync(names[1]);
        await Expect(PublicRow(verifyPage, 0)).ToContainTextAsync(names[0]);

        await CapturePublicScreenshotsAsync(verifyPage);
    }

    [Fact]
    public async Task FeaturedEditorKeepsItsControlsInsideTheSlotsAtDesktopAndIntermediateViewports()
    {
        var admin = await App.CreatePersonaAsync("featured-layout-admin", isAdmin: true);
        using var adminApi = App.CreateApiClient(admin);
        await SeedFeaturedAsync(adminApi);

        await using var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/");
        await OpenEditorAsync(page);

        // 641-900px is where the card list becomes two columns with narrower cards, so the editing
        // controls are the tightest there; 390px is the mobile stack.
        foreach (var (width, height) in new[]
                 {
                     (1440, 900), (1024, 900), (900, 900), (820, 900), (768, 900), (700, 900), (641, 900), (390, 844)
                 })
        {
            await page.SetViewportSizeAsync(width, height);
            await AssertEditorControlsStayInsideSlotsAsync(page, width);

            if (width is 1440 or 820 or 768 or 390)
                await CaptureEditorAsync(page, $"editor-{width}-light-en");
        }
    }

    [Fact]
    public async Task RemoveAddAndTheCompactPositionSelectorEditOnlyTheDraftAndCancelKeepsTheSavedOrder()
    {
        var admin = await App.CreatePersonaAsync("featured-draft-admin", isAdmin: true);
        using var adminApi = App.CreateApiClient(admin);
        var names = await SeedFeaturedAsync(adminApi);

        await using var context = await App.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await TournamentE2E.GotoInteractiveAsync(page, "/");
        await OpenEditorAsync(page);

        var heroPrimary = page.Locator(".home-hero-media-image--primary");
        var heroSourceBeforeDraft = await heroPrimary.GetAttributeAsync("src");

        // Remove the last card: the slot empties and saving stays unavailable with three positions.
        var lastSlot = EditorRow(page, 2);
        await lastSlot.Locator("button.home-tournament-slot-remove").ClickWhenInteractiveAsync();
        await Expect(lastSlot.Locator(".home-tournament-slot-empty-copy")).ToBeVisibleAsync();
        await Expect(SaveButton(page)).ToBeDisabledAsync();

        // Add it back from the available list; saving becomes available again.
        await page.Locator(".featured-editor-chip").Filter(new() { HasText = names[3] }).ClickWhenInteractiveAsync();
        await Expect(EditorRow(page, 2)).ToContainTextAsync(names[3]);
        await Expect(SaveButton(page)).ToBeEnabledAsync();

        // The compact position selector is the non-drag alternative: promote the third card to lead.
        await EditorRow(page, 1).Locator("select.home-tournament-slot-select").SelectOptionAsync("1");
        await Expect(EditorLead(page)).ToContainTextAsync(names[2]);

        // Still draft only.
        Assert.Equal(heroSourceBeforeDraft, await heroPrimary.GetAttributeAsync("src"));

        await CancelEditorAsync(page);
        await Expect(PublicLead(page)).ToContainTextAsync(names[0]);
        Assert.Equal(heroSourceBeforeDraft, await heroPrimary.GetAttributeAsync("src"));

        // Reopening shows the saved arrangement, so cancel did not persist the draft.
        await OpenEditorAsync(page);
        await Expect(EditorLead(page)).ToContainTextAsync(names[0]);
        await Expect(EditorRow(page, 0)).ToContainTextAsync(names[1]);
        await Expect(EditorRow(page, 1)).ToContainTextAsync(names[2]);
        await Expect(EditorRow(page, 2)).ToContainTextAsync(names[3]);
    }

    [Fact]
    public async Task AnonymousAndNonAdminVisitorsCannotEditTheFeaturedTournaments()
    {
        var admin = await App.CreatePersonaAsync("featured-guard-admin", isAdmin: true);
        using var adminApi = App.CreateApiClient(admin);
        var names = await SeedFeaturedAsync(adminApi);

        // Anonymous visitors get the public cards without any editor entry point.
        await using (var anonymousContext = await App.NewContextAsync())
        {
            var page = await anonymousContext.NewPageAsync();
            await TournamentE2E.GotoInteractiveAsync(page, "/");
            await Expect(PublicLead(page)).ToContainTextAsync(names[0]);
            await Expect(EditButton(page)).ToHaveCountAsync(0);
            await Expect(page.Locator(".featured-editor")).ToHaveCountAsync(0);
        }

        // An authenticated non-administrator is treated the same way.
        var member = await App.CreatePersonaAsync("featured-guard-member");
        await using (var memberContext = await App.NewAuthenticatedContextAsync(member))
        {
            var page = await memberContext.NewPageAsync();
            await TournamentE2E.GotoInteractiveAsync(page, "/");
            await Expect(PublicLead(page)).ToContainTextAsync(names[0]);
            await Expect(EditButton(page)).ToHaveCountAsync(0);
            await Expect(page.Locator(".featured-editor")).ToHaveCountAsync(0);
        }

        // The write itself stays administrator-only: a non-administrator PUT is refused and the
        // stored arrangement is unchanged.
        using var memberApi = App.CreateApiClient(member);
        using var rejected = await TournamentE2E.SendJsonAsync(
            memberApi,
            HttpMethod.Put,
            "v1/lan/featured-tournaments",
            new { tournamentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() } });
        Assert.True(
            rejected.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected the non-administrator save to be refused but the API answered {(int)rejected.StatusCode}.");

        var stored = await TournamentE2E.ReadJsonAsync(await adminApi.GetAsync("v1/lan/featured-tournaments"));
        Assert.Equal(
            names,
            stored.GetProperty("tournaments").EnumerateArray()
                .Select(item => item.GetProperty("name").GetString()!)
                .Take(4)
                .ToArray());
    }

    /// <summary>
    /// Creates four scheduled tournaments and persists them in that order, so each assertion starts
    /// from a known arrangement instead of whichever tournaments happen to exist.
    /// </summary>
    private async Task<string[]> SeedFeaturedAsync(HttpClient adminApi)
    {
        var names = Enumerable.Range(1, 4)
            .Select(index => TournamentE2E.Unique($"E2E Featured P{index}"))
            .ToArray();

        var ids = new List<Guid>();
        foreach (var name in names)
            ids.Add(await TournamentE2E.CreateTournamentAsync(adminApi, name, "SingleElimination", "Individual"));

        using var response = await TournamentE2E.SendJsonAsync(
            adminApi, HttpMethod.Put, "v1/lan/featured-tournaments", new { tournamentIds = ids });
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Seeding the featured order failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }

        var featured = await TournamentE2E.ReadJsonAsync(await adminApi.GetAsync("v1/lan/featured-tournaments"));
        var order = featured.GetProperty("tournaments").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .Take(4)
            .ToArray();
        Assert.Equal(names, order);

        return names;
    }

    private static ILocator PublicLead(IPage page) => page.Locator("#featured-tournaments .home-tournament-lead");

    private static ILocator PublicRow(IPage page, int index) =>
        page.Locator("#featured-tournaments .home-tournament-row").Nth(index);

    private static ILocator EditorLead(IPage page) =>
        page.Locator(".featured-editor .home-tournament-lead.home-tournament-slot");

    private static ILocator EditorRow(IPage page, int index) =>
        page.Locator(".featured-editor .home-tournament-row.home-tournament-slot").Nth(index);

    private static ILocator EditButton(IPage page) =>
        page.Locator(".home-section-actions").GetByRole(AriaRole.Button, new() { Name = Text.Edit, Exact = true });

    private static ILocator SaveButton(IPage page) =>
        page.Locator(".featured-editor-actions")
            .GetByRole(AriaRole.Button, new() { Name = Text.SaveChanges, Exact = true });

    private static ILocator CancelButton(IPage page) =>
        page.Locator(".featured-editor-actions")
            .GetByRole(AriaRole.Button, new() { Name = Text.Cancel, Exact = true });

    private static async Task OpenEditorAsync(IPage page)
    {
        await EditButton(page).ClickWhenInteractiveAsync();
        await Expect(page.Locator(".featured-editor")).ToBeVisibleAsync();
        await Expect(EditorLead(page)).ToBeVisibleAsync();
    }

    private static async Task SaveAsync(IPage page)
    {
        await SaveButton(page).ClickWhenInteractiveAsync();
        await Expect(page.Locator(".featured-editor")).ToHaveCountAsync(0);
    }

    private static async Task CancelEditorAsync(IPage page)
    {
        await CancelButton(page).ClickWhenInteractiveAsync();
        await Expect(page.Locator(".featured-editor")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// The editing cards must not overflow horizontally at this width, and every control inside a
    /// slot must stay inside that slot's box. Scoped to the editor so the public card list keeps its
    /// own layout contract.
    /// </summary>
    private static async Task AssertEditorControlsStayInsideSlotsAsync(IPage page, int width)
    {
        var editor = page.Locator(".featured-editor");
        var slots = editor.Locator(".home-tournament-slot");
        var slotCount = await slots.CountAsync();
        Assert.Equal(4, slotCount);

        var editorOverflows = await editor.EvaluateAsync<bool>(
            "element => element.scrollWidth > element.clientWidth + 2");
        Assert.False(editorOverflows, $"The featured editor overflows horizontally at {width}px.");

        for (var index = 0; index < slotCount; index++)
        {
            var slot = slots.Nth(index);
            var slotOverflows = await slot.EvaluateAsync<bool>(
                "element => element.scrollWidth > element.clientWidth + 2");
            Assert.False(slotOverflows, $"Featured slot {index + 1} overflows its own card at {width}px.");

            var slotBox = await slot.BoundingBoxAsync();
            Assert.NotNull(slotBox);

            var controls = slot.Locator(
                ".home-tournament-slot-controls, .home-tournament-slot-remove, .home-tournament-slot-select");
            var controlCount = await controls.CountAsync();
            Assert.True(controlCount > 0, $"Featured slot {index + 1} rendered no editing control at {width}px.");

            for (var control = 0; control < controlCount; control++)
            {
                var controlBox = await controls.Nth(control).BoundingBoxAsync();
                Assert.NotNull(controlBox);
                Assert.True(
                    controlBox!.X >= slotBox!.X - 2 &&
                    controlBox.X + controlBox.Width <= slotBox.X + slotBox.Width + 2 &&
                    controlBox.Y >= slotBox.Y - 2 &&
                    controlBox.Y + controlBox.Height <= slotBox.Y + slotBox.Height + 2,
                    $"A control of featured slot {index + 1} escapes the card at {width}px.");
            }
        }
    }

    private static async Task CapturePublicScreenshotsAsync(IPage page)
    {
        if (!ScreenshotsEnabled)
            return;

        foreach (var (viewport, theme, culture, label) in new[]
                 {
                     ((1400, 900), "light", "en-US", "public-desktop-light-en"),
                     ((1400, 900), "dark", "en-US", "public-desktop-dark-en"),
                     ((1400, 900), "light", "nl-BE", "public-desktop-light-nl"),
                     ((390, 844), "light", "en-US", "public-mobile-light-en"),
                     ((390, 844), "dark", "en-US", "public-mobile-dark-en"),
                     ((390, 844), "light", "nl-BE", "public-mobile-light-nl")
                 })
        {
            await SetThemeAsync(page, theme);
            await SetLanguageAsync(page, culture);
            await CaptureAsync(page, label, viewport);
        }

        await SetThemeAsync(page, "light");
        await SetLanguageAsync(page, "en-US");
    }

    private static async Task SetThemeAsync(IPage page, string state)
    {
        var shell = page.Locator(".layout-shell");
        if (string.Equals(await shell.GetAttributeAsync("data-theme-state"), state, StringComparison.Ordinal))
            return;

        await page.ClickWhenInteractiveAsync(page.Locator(".theme-toggle"));
        await Expect(shell).ToHaveAttributeAsync("data-theme-state", state);
    }

    private static async Task SetLanguageAsync(IPage page, string culture)
    {
        if (await page.Locator("#footer-language-select").InputValueAsync() == culture)
            return;

        await page.SelectOptionAsync("#footer-language-select", culture);
        await Expect(page.Locator("#footer-language-select")).ToHaveValueAsync(culture);
    }

    /// <summary>
    /// Screenshots are opt-in through MERCURIUS_E2E_SCREENSHOT_DIR so the durable suite stays quiet
    /// by default and writes artifacts wherever the caller points it.
    /// </summary>
    private static bool ScreenshotsEnabled =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MERCURIUS_E2E_SCREENSHOT_DIR"));

    /// <summary>
    /// Clipped capture of the editing surface. The editor is scrolled into view and its four slots,
    /// a grip and the Save/Cancel row are asserted visible first, so the image cannot record a
    /// pre-paint frame whose secondary cards are still unpainted.
    /// </summary>
    private static async Task CaptureEditorAsync(IPage page, string label)
    {
        if (!ScreenshotsEnabled)
            return;

        var root = Environment.GetEnvironmentVariable("MERCURIUS_E2E_SCREENSHOT_DIR")!;
        var editor = page.Locator(".featured-editor");
        await Expect(editor.Locator(".home-tournament-slot")).ToHaveCountAsync(4);
        await Expect(editor.Locator(".home-tournament-slot-grip").First).ToBeVisibleAsync();
        await Expect(SaveButton(page)).ToBeVisibleAsync();
        await Expect(CancelButton(page)).ToBeVisibleAsync();
        await editor.ScrollIntoViewIfNeededAsync();

        Directory.CreateDirectory(root);
        await editor.ScreenshotAsync(new() { Path = Path.Combine(root, $"{label}.png") });
    }

    private static async Task CaptureAsync(IPage page, string label, (int Width, int Height)? viewport = null)
    {
        var root = Environment.GetEnvironmentVariable("MERCURIUS_E2E_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(root))
            return;

        if (viewport is { } size)
            await page.SetViewportSizeAsync(size.Width, size.Height);

        Directory.CreateDirectory(root);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(root, $"{label}.png"),
            FullPage = true
        });
    }

    private static class Text
    {
        public const string Edit = "Edit";
        public const string SaveChanges = "Save changes";
        public const string Cancel = "Cancel";
    }
}
