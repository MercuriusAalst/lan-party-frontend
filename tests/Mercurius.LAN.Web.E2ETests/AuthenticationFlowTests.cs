using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Authentication shell and authorization boundaries: the real sign-in / sign-out loop
/// against the test identity provider, protected-route redirects, role-gated navigation
/// and the forbidden/not-found states.
/// </summary>
[Collection(E2ECollection.Name)]
public class AuthenticationFlowTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task AnonymousVisitorIsChallengedByTheIdentityProviderForProtectedRoutes()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}profile");

        await Expect(page.GetByText("Mercurius E2E sign in")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Cancel sign in" })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("users/e2e-protected-profile")]
    public async Task CancellingSignInFromAProtectedRouteReturnsHomeAsAnonymousVisitor(string protectedPath)
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}{protectedPath}");
        // The identity provider page is plain server HTML with no Blazor circuit, so it is clicked
        // directly instead of through the interactivity-gated helper.
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel sign in" }).ClickAsync();

        await Expect(page.GetByText("Login was cancelled.")).ToBeVisibleAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"^{Regex.Escape(app.BaseUrl)}$"));
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();
        await Expect(AccountMenuButton(page)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task CancelledLoginQueryShowsInformationalToastAndCleansUrl()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}?login=cancelled");

        await Expect(page.GetByText("Login was cancelled.")).ToBeVisibleAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"{Regex.Escape(app.BaseUrl)}$"));
    }

    [Fact]
    public async Task FailedLoginQueryShowsFailureToastAndCleansUrl()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}?login=failed");

        await Expect(page.GetByText("Login failed. Please try again.")).ToBeVisibleAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"{Regex.Escape(app.BaseUrl)}$"));
    }

    [Fact]
    public async Task FailedRegistrationQueryShowsRegistrationToastAndCleansUrl()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}?login=failed&registration=true");

        await Expect(page.GetByText("Registration failed. Please try again.")).ToBeVisibleAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"{Regex.Escape(app.BaseUrl)}$"));
    }

    [Fact]
    public async Task MemberSignsInThroughTheLoginUiAndSignsOutAgain()
    {
        var member = await app.CreatePersonaAsync("auth-signin");

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Log in" }));
        await app.SelectPersonaAsync(page, member);

        await Expect(AccountMenuButton(page)).ToBeVisibleAsync();
        await Expect(page.Locator($"#account-nav-menu-container button[aria-label*='{member.Username}']")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToHaveCountAsync(0);

        await OpenAccountMenuAsync(page);
        await page.ClickWhenInteractiveAsync(page.Locator("#account-nav-menu-container").GetByRole(AriaRole.Button, new() { Name = "Logout" }));

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();
        await Expect(AccountMenuButton(page)).ToHaveCountAsync(0);

        await page.GotoAsync($"{app.BaseUrl}profile");
        await Expect(page.GetByText("Mercurius E2E sign in")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminSeesOrganizerToolsAndCanOpenSponsorManagement()
    {
        var admin = await app.CreatePersonaAsync("auth-admin", isAdmin: true);

        await using var context = await app.NewAuthenticatedContextAsync(admin);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Admin", Exact = true }));
        var adminMenu = page.Locator("#admin-nav-menu");
        await Expect(adminMenu).ToBeVisibleAsync();
        // The sponsor entry renders with role="menuitem", so it is not matched as a link.
        await page.ClickWhenInteractiveAsync(adminMenu.GetByRole(AriaRole.Menuitem, new() { Name = "Sponsors" }));

        await Expect(page).ToHaveURLAsync(new Regex("/admin/sponsors$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Manage sponsors" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task MemberHasNoOrganizerToolsAndIsForbiddenOnAdminRoute()
    {
        var member = await app.CreatePersonaAsync("auth-member-admin-route");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        await Expect(AccountMenuButton(page)).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Admin", Exact = true })).ToHaveCountAsync(0);

        var response = await page.GotoAsync($"{app.BaseUrl}admin/sponsors");

        // The status-code middleware re-executes the forbidden page while the address bar keeps
        // the protected URL, so assert the status code and the rendered recovery state.
        Assert.Equal((int)HttpStatusCode.Forbidden, response!.Status);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Forbidden" })).ToBeVisibleAsync();
        await Expect(page.GetByText("You do not have permission to access this page.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Back Home" })).ToBeVisibleAsync();

        await page.AssertShippedShellStaysInteractiveAsync();
    }

    [Fact]
    public async Task ForbiddenPageFromDirectRequestStaysInteractiveThroughInCircuitNavigation()
    {
        var member = await app.CreatePersonaAsync("auth-403-in-circuit");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();

        // Reaching the forbidden page through a denied direct request must not leave a broken
        // circuit behind: the shell controls keep working and client-side navigation still runs
        // without tearing the renderer down.
        var response = await page.GotoAsync($"{app.BaseUrl}admin/sponsors");
        Assert.Equal((int)HttpStatusCode.Forbidden, response!.Status);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Forbidden" })).ToBeVisibleAsync();

        await page.AssertShippedShellStaysInteractiveAsync();

        // The NavLink navigates client-side, so a successful trip proves the circuit is still alive.
        await page.ClickWhenInteractiveAsync(page.Locator("#primary-navigation").GetByRole(AriaRole.Link, new() { Name = "Tournaments" }));
        await Expect(page).ToHaveURLAsync(new Regex("/tournaments$"));
        await Expect(page.Locator(".tournaments-browse-section")).ToBeVisibleAsync();

        Assert.Empty(page.FatalConsoleMessages());
    }

    [Fact]
    public async Task MemberSearchResultForAnotherUserIsDisabledInGlobalSearch()
    {
        var member = await app.CreatePersonaAsync("auth-search-member");
        var admin = await app.CreatePersonaAsync("auth-search-admin", isAdmin: true);
        var otherUsername = $"searchtarget{Guid.NewGuid():N}"[..18];

        // Personas created by the fixture are inserted straight into the database, so they never
        // publish the profile integration event the discovery index consumes. Creating the
        // searchable user through the real admin endpoint exercises that projection path.
        using (var adminApi = app.CreateApiClient(admin))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/lan/users")
            {
                Content = JsonContent.Create(new
                {
                    auth0UserId = $"auth0|e2e-search-{Guid.NewGuid():N}",
                    username = otherUsername,
                    firstname = "Search",
                    lastname = "Target",
                    emailVerified = true
                })
            };
            var response = await adminApi.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        await WaitForUserInSearchIndexAsync(otherUsername);
        await page.SearchWhenInteractiveAsync(otherUsername);

        var options = page.Locator("#global-nav-search-results button[role='option']");
        await Expect(options.First).ToBeVisibleAsync();
        var target = options.Filter(new() { HasText = otherUsername }).First;
        await Expect(target).ToBeVisibleAsync();
        await Expect(target).ToBeDisabledAsync();
    }

    [Fact]
    public async Task MemberCannotOpenTheAdminOnlyUserProfileEvenThoughThePublicApiListsTheUsername()
    {
        var member = await app.CreatePersonaAsync("auth-user-profile-member");
        var other = await app.CreatePersonaAsync("auth-user-profile-target");
        var otherUsername = $"searchtarget{Guid.NewGuid():N}"[..18];
        using (var otherApi = app.CreateApiClient(other))
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "v1/lan/users/me")
            {
                Content = JsonContent.Create(new
                {
                    username = otherUsername,
                    firstname = "Search",
                    lastname = "Target"
                })
            };
            var update = await otherApi.SendAsync(request);
            update.EnsureSuccessStatusCode();
        }

        // The anonymous public endpoint deliberately exposes nothing but the username, yet the only
        // page that consumes user profiles is admin-only. A member reaching it directly is refused.
        var publicResponse = await app.Api.GetAsync($"v1/lan/public/users/{otherUsername}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        using (var document = JsonDocument.Parse(await publicResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal(otherUsername, document.RootElement.GetProperty("username").GetString());
            Assert.False(document.RootElement.TryGetProperty("firstname", out _));
            Assert.False(document.RootElement.TryGetProperty("discordId", out _));
        }

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{app.BaseUrl}users/{otherUsername}");

        // Same static-SSR authorization gap: the anonymous public endpoint proves the data shape,
        // while the admin-only page must render the Forbidden StatusPage for a member.
        Assert.Equal((int)HttpStatusCode.Forbidden, response!.Status);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Forbidden" })).ToBeVisibleAsync();
        await Expect(page.GetByText("You do not have permission to access this page.")).ToBeVisibleAsync();

        // The forbidden page must keep a live circuit, not just correct SSR markup.
        await page.AssertShippedShellStaysInteractiveAsync();
    }

    [Fact]
    public async Task AuthenticatedMemberVisitingUnknownRouteSeesNotFoundPage()
    {
        var member = await app.CreatePersonaAsync("auth-unknown-route");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{app.BaseUrl}not-a-real-page");

        // Observed on .NET 10 static SSR: unknown routes answer 404 with an empty document instead of
        // the router's NotFound StatusPage (trace 008.zip: 404 /not-a-real-page, no .status-page DOM).
        // Kept as the correct expectation rather than adjusted to the broken response.
        Assert.Equal((int)HttpStatusCode.NotFound, response!.Status);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Page Not Found" })).ToBeVisibleAsync();
        await Expect(page.GetByText("The page you requested does not exist or has moved.")).ToBeVisibleAsync();

        // The not-found recovery page renders through the router's own LayoutView; like the
        // forbidden page it must leave the shell interactive when reached by a direct request.
        await page.AssertShippedShellStaysInteractiveAsync();
    }

    [Fact]
    public async Task AuthenticatedMemberNotificationMenuShowsTheEmptyState()
    {
        var member = await app.CreatePersonaAsync("auth-notifications");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Notifications" }));

        await Expect(page.Locator(".nav-notification-dropdown")).ToBeVisibleAsync();
        await Expect(page.GetByText("No notifications.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Mark read" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task DeepLinkedProtectedRouteReturnsToTheRequestedPageAfterSignIn()
    {
        var member = await app.CreatePersonaAsync("auth-deep-link");

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}profile");

        // RedirectToLogin must keep the requested path in the return URL all the way through the
        // identity provider so the visitor lands on /profile instead of the home page.
        await Expect(page.GetByText("Mercurius E2E sign in")).ToBeVisibleAsync();
        await app.SelectPersonaAsync(page, member);

        await Expect(page).ToHaveURLAsync(new Regex("/profile$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your information" })).ToBeVisibleAsync();
        await Expect(AccountMenuButton(page)).ToBeVisibleAsync();
    }

    internal static ILocator AccountMenuButton(IPage page) =>
        page.Locator("#account-nav-menu-container button[aria-label$='account menu']");

    internal static async Task OpenAccountMenuAsync(IPage page)
    {
        await page.ClickWhenInteractiveAsync(AccountMenuButton(page));
        await Expect(page.Locator("#account-nav-menu-container .nav-user-menu")).ToBeVisibleAsync();
    }

    /// <summary>
    /// The community search index is an eventually consistent outbox projection maintained by a
    /// background worker, so wait for the real search endpoint to expose the seeded persona before
    /// asserting what the browser does with that result.
    /// </summary>
    private async Task WaitForUserInSearchIndexAsync(string username)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var response = await app.Api.GetAsync($"v1/lan/search?query={Uri.EscapeDataString(username)}");
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (document.RootElement.TryGetProperty("results", out var results) &&
                    results.EnumerateArray().Any(item =>
                        item.TryGetProperty("username", out var value) &&
                        string.Equals(value.GetString(), username, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The discovery search index did not expose '{username}' within 30 seconds.");
    }
}
