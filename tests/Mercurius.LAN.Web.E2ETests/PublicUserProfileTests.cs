using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// The participant profile page at /users/{username}. Despite the public-sounding URL it is
/// gated to the admin role and consumes the admin identity endpoints, so the reachable states
/// are: admin with data, admin with an unknown username, and refused/re-challenged for everyone else.
/// </summary>
[Collection(E2ECollection.Name)]
public class PublicUserProfileTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task AdminOpensParticipantProfileWithLinkedIdentitiesAndEmptyMatchHistory()
    {
        var admin = await app.CreatePersonaAsync("user-profile-admin", isAdmin: true);
        var target = await app.CreatePersonaAsync("user-profile-target");
        var profileUsername = $"linktarget{Guid.NewGuid():N}"[..18];

        using (var targetApi = app.CreateApiClient(target))
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "v1/lan/users/me")
            {
                Content = JsonContent.Create(new
                {
                    username = profileUsername,
                    firstname = "Link",
                    lastname = "Target",
                    discordId = "discord-e2e-1",
                    steamId = "steam-e2e-1",
                    riotId = "riot-e2e-1"
                })
            };
            var update = await targetApi.SendAsync(request);
            update.EnsureSuccessStatusCode();
        }

        await using var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{app.BaseUrl}users/{profileUsername}");
        Assert.Equal((int)HttpStatusCode.OK, response!.Status);

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Link Target", Level = 1 })).ToBeVisibleAsync();
        await Expect(page.GetByText("Participant profile")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Profile details" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Linked IDs" })).ToBeVisibleAsync();
        await Expect(page.GetByText("discord-e2e-1")).ToBeVisibleAsync();
        await Expect(page.GetByText("steam-e2e-1")).ToBeVisibleAsync();
        await Expect(page.GetByText("riot-e2e-1")).ToBeVisibleAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Matches" })).ToBeVisibleAsync();
        await Expect(page.GetByText("No completed match is listed yet.")).ToBeVisibleAsync();
        await Expect(page.GetByText("No upcoming match is scheduled.")).ToBeVisibleAsync();

        // The page renders usernames, names and linked game IDs only; the identity email stays private.
        await Expect(page.GetByText(target.Email)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task UnknownUsernameShowsTheUnavailableStateRatherThanNotFound()
    {
        var admin = await app.CreatePersonaAsync("user-profile-admin-missing", isAdmin: true);
        var missingUsername = $"ghost{Guid.NewGuid():N}"[..18];

        await using var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}users/{missingUsername}");

        // The component treats every API failure, including the 404 for an unknown username, as a
        // load error, so this is the observable state and the localized "User not found" branch
        // below it is unreachable from the UI.
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Profile unavailable" })).ToBeVisibleAsync();
        await Expect(page.GetByText("This user profile could not be loaded right now.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "User not found" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AnonymousVisitorIsChallengedBeforeReachingTheParticipantProfile()
    {
        var target = await app.CreatePersonaAsync("user-profile-anon-target");

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}users/{target.Username}");

        await Expect(page.GetByText("Mercurius E2E sign in")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Cancel sign in" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminSeesCompletedAndUpcomingMatchSummariesForAParticipant()
    {
        var admin = await app.CreatePersonaAsync("user-profile-matches-admin", isAdmin: true);
        using var adminApi = app.CreateApiClient(admin);
        var players = await MatchE2E.CreatePersonasAsync(app, "profile-match-player", 4);
        var tournamentId = await MatchE2E.StartIndividualTournamentAsync(app, adminApi, players, "SingleElimination");

        // Completing both semi-finals promotes the two winners into the final, which gives the
        // subject one completed match and one assigned-but-unstarted match in the same tournament.
        var roundOne = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId))
            .Where(match => match.RoundNumber == 1)
            .OrderBy(match => match.MatchNumber)
            .ToList();
        Assert.Equal(2, roundOne.Count);
        foreach (var match in roundOne)
        {
            await MatchE2E.CompleteMatchViaApiAsync(
                app,
                match.Id,
                MatchE2E.PersonaForUser(players, match.UserParticipant1Id),
                MatchE2E.PersonaForUser(players, match.UserParticipant2Id),
                1,
                0);
        }

        var final = (await MatchE2E.GetMatchesAsync(app.Api, tournamentId))
            .Single(match => match.RoundNumber == 2);
        var subject = MatchE2E.PersonaForUser(players, final.UserParticipant1Id);
        var upcomingOpponent = MatchE2E.PersonaForUser(players, final.UserParticipant2Id);
        var subjectRoundOne = roundOne.Single(match =>
            match.UserParticipant1Id == subject.UserId || match.UserParticipant2Id == subject.UserId);
        var previousOpponent = MatchE2E.PersonaForUser(
            players,
            subjectRoundOne.UserParticipant1Id == subject.UserId
                ? subjectRoundOne.UserParticipant2Id
                : subjectRoundOne.UserParticipant1Id);
        var expectedScore = subjectRoundOne.UserParticipant1Id == subject.UserId
            ? "participant 1 - 0 opponent"
            : "participant 0 - 1 opponent";
        var tournament = await TournamentE2E.ReadJsonAsync(await app.Api.GetAsync($"v1/lan/tournaments/{tournamentId}"));
        var tournamentName = tournament.GetProperty("name").GetString()!;

        await using var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{app.BaseUrl}users/{subject.Username}");
        Assert.Equal((int)HttpStatusCode.OK, response!.Status);

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Matches" })).ToBeVisibleAsync();
        await Expect(page.GetByText("No completed match is listed yet.")).ToHaveCountAsync(0);
        await Expect(page.GetByText("No upcoming match is scheduled.")).ToHaveCountAsync(0);

        var previousGroup = page.Locator(".public-profile-match-summary-group").Filter(new() { HasText = "Previous" });
        var previousCard = previousGroup.Locator(".public-profile-match-summary-card");
        await Expect(previousCard).ToHaveCountAsync(1);
        await Expect(previousCard).ToContainTextAsync(tournamentName);
        await Expect(previousCard).ToContainTextAsync($"vs {previousOpponent.Username}");
        await Expect(previousCard).ToContainTextAsync(expectedScore);
        await Expect(previousCard).ToContainTextAsync("Upper bracket, round 1");
        await Expect(previousCard).ToHaveAttributeAsync("href", $"/tournaments/{tournamentId}");

        var upcomingGroup = page.Locator(".public-profile-match-summary-group").Filter(new() { HasText = "Upcoming" });
        var upcomingCard = upcomingGroup.Locator(".public-profile-match-summary-card");
        await Expect(upcomingCard).ToHaveCountAsync(1);
        await Expect(upcomingCard).ToContainTextAsync(tournamentName);
        await Expect(upcomingCard).ToContainTextAsync($"vs {upcomingOpponent.Username}");
        await Expect(upcomingCard).ToContainTextAsync("Upper bracket, round 2");
        await Expect(upcomingCard.Locator(".public-profile-match-summary-result"))
            .ToContainTextAsync(new Regex("^(Scheduled match|Awaiting start .* estimate passed)$"));
        await Expect(upcomingCard).ToHaveAttributeAsync("href", $"/tournaments/{tournamentId}");
    }

    [Fact]
    public async Task AdminProfileMatchHistoryFailureShowsUnavailableStateAndRetryRecovers()
    {
        var admin = await app.CreatePersonaAsync("user-profile-fault-admin", isAdmin: true);
        var target = await app.CreatePersonaAsync("user-profile-fault-target");

        await using var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();

        await using (var fault = await DatabaseReadFault.InstallAsync(app, "matches"))
        {
            var response = await page.GotoAsync($"{app.BaseUrl}users/{target.Username}");
        Assert.Equal((int)HttpStatusCode.OK, response!.Status);

            // The profile itself still renders from the identity table; only the match projection fails.
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Profile details" }))
                .ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(page.GetByText("Match history is temporarily unavailable. The rest of this public profile is still available."))
                .ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Retry match history" })).ToBeVisibleAsync();
        }

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Retry match history" }));

        await Expect(page.GetByText("Match history is temporarily unavailable. The rest of this public profile is still available."))
            .ToHaveCountAsync(0);
        await Expect(page.GetByText("No completed match is listed yet.")).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(page.GetByText("No upcoming match is scheduled.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminOpensUserResultFromGlobalSearch()
    {
        var admin = await app.CreatePersonaAsync("user-search-result-admin", isAdmin: true);
        var username = $"searchresult{Guid.NewGuid():N}"[..18];

        // Fixture personas are inserted straight into the database, so the discovery projection only
        // fires for a profile created through the real admin endpoint. This is a distinct new user.
        using (var adminApi = app.CreateApiClient(admin))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/lan/users")
            {
                Content = JsonContent.Create(new
                {
                    auth0UserId = $"auth0|e2e-search-result-{Guid.NewGuid():N}",
                    username,
                    firstname = "Search",
                    lastname = "Result",
                    emailVerified = true
                })
            };
            var created = await adminApi.SendAsync(request);
            created.EnsureSuccessStatusCode();
        }

        await PublicSiteTests.WaitForSearchResultAsync(app, username, item =>
            item.TryGetProperty("username", out var value) &&
            string.Equals(value.GetString(), username, StringComparison.OrdinalIgnoreCase));

        await using var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();
        var searchInput = page.Locator("#global-nav-search");
        await Expect(searchInput).ToBeVisibleAsync();
        Assert.True(
            await page.Locator(".brand-header-inner").EvaluateAsync<bool>("header => header.scrollWidth <= header.clientWidth"),
            "The desktop header should not overflow horizontally.");
        await searchInput.FillAsync(username);

        var option = page.Locator("#global-nav-search-results button[role='option']").Filter(new() { HasText = username });
        await Expect(option).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(option);

        await Expect(page).ToHaveURLAsync(new Regex($"/users/{username}$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Profile details" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(page.GetByText(username).First).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(1025)]
    [InlineData(1100)]
    [InlineData(1440)]
    public async Task AuthenticatedHeaderKeepsSearchUsableWithoutHorizontalOverflow(int viewportWidth)
    {
        var admin = await app.CreatePersonaAsync($"header-width-{viewportWidth}-admin", isAdmin: true);
        var tournamentName = TournamentE2E.Unique("E2E Header Width");
        using (var adminApi = app.CreateApiClient(admin))
        {
            await TournamentE2E.CreateTournamentAsync(adminApi, tournamentName, "SingleElimination", "Individual");
        }

        // The discovery projection is asynchronous; wait for the index to expose the tournament
        // before driving the UI, otherwise the search box legitimately reports no matches.
        await PublicSiteTests.WaitForSearchResultAsync(app, tournamentName, item =>
            item.TryGetProperty("displayLabel", out var value) &&
            string.Equals(value.GetString(), tournamentName, StringComparison.OrdinalIgnoreCase));

        await using var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(viewportWidth, 900);
        await page.GotoAsync(app.BaseUrl);
        await page.WaitForInteractiveAsync();

        var searchInput = page.Locator("#global-nav-search");
        await Expect(searchInput).ToBeVisibleAsync();

        // Desktop navigation is expanded above the 1024px collapse breakpoint: the admin controls,
        // the account widget, and the organizer menu must all be present and readable.
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Admin" })).ToBeVisibleAsync();
        await Expect(page.Locator(".nav-user-widget-controls")).ToBeVisibleAsync();
        await Expect(page.Locator(".theme-toggle")).ToBeVisibleAsync();
        await Expect(page.Locator(".menu-toggle")).ToBeHiddenAsync();
        await Expect(page.Locator(".brand-nav-links")).ToHaveCSSAsync("flex-direction", "row");

        // Real geometry: nothing in the header may exceed the header's own content box, and no
        // header child may be pushed past the right edge of the viewport.
        var geometry = await page.Locator(".brand-header-inner").EvaluateAsync<string>(
            @"header => {
                const box = header.getBoundingClientRect();
                const children = [...header.querySelectorAll('img, button, input, a')];
                const worstRight = Math.max(...children.map(c => c.getBoundingClientRect().right));
                return JSON.stringify({
                    scrollWidth: header.scrollWidth,
                    clientWidth: header.clientWidth,
                    headerRight: box.right,
                    worstChildRight: worstRight,
                    viewport: window.innerWidth
                });
            }");
        using var metrics = JsonDocument.Parse(geometry);
        var root = metrics.RootElement;
        Assert.True(
            root.GetProperty("scrollWidth").GetInt32() <= root.GetProperty("clientWidth").GetInt32(),
            $"The authenticated header should not overflow horizontally at {viewportWidth}px: {geometry}");
        Assert.True(
            root.GetProperty("worstChildRight").GetDouble() <= root.GetProperty("viewport").GetInt32(),
            $"No header control may be pushed past the viewport at {viewportWidth}px: {geometry}");

        // The search field must keep real text room. `.brand-nav-search` is the flex item the header
        // competes for; once it can no longer yield, the field collapses to the magnifier.
        var searchFieldWidth = await page.Locator(".brand-nav-search")
            .EvaluateAsync<double>("field => field.getBoundingClientRect().width");
        Assert.True(
            searchFieldWidth >= 120,
            $"The authenticated desktop search field should keep usable text room at {viewportWidth}px but was {searchFieldWidth}px.");

        // The field has to actually work, not just accept keystrokes: a real query returns a real result.
        await searchInput.FillAsync(tournamentName);
        var option = page.Locator("#global-nav-search-results button[role='option']").Filter(new() { HasText = tournamentName });
        await Expect(option).ToBeVisibleAsync(new() { Timeout = 15000 });
    }
}
