using System.Text.RegularExpressions;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Identity lifecycle for an authenticated player: the multi-step onboarding that creates the
/// profile row, profile editing and validation, the Auth0-backed account actions and the
/// destructive delete-account confirmation.
/// </summary>
[Collection(E2ECollection.Name)]
public class ProfileAndOnboardingTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task AuthenticatedUserWithoutProfileIsPushedIntoOnboarding()
    {
        var persona = await app.CreatePersonaAsync("onboard-gate", createProfile: false);

        await using var context = await app.NewAuthenticatedContextAsync(persona);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        await Expect(page).ToHaveURLAsync(new Regex("/complete-profile\\?returnUrl=%2F$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Complete your profile" })).ToBeVisibleAsync();
        await Expect(page.Locator("#username")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task OnboardingBlocksBlankUsernameAndKeepsUserOnAccountStep()
    {
        var persona = await app.CreatePersonaAsync("onboard-blank", createProfile: false);

        await using var context = await app.NewAuthenticatedContextAsync(persona);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}complete-profile");
        await page.WaitForInteractiveAsync();

        // The identity provider prefills the username claim, so blank validation only fires after
        // the field is cleared explicitly.
        await page.FillAsync("#username", string.Empty);
        await page.ClickWhenInteractiveAsync(ContinueButton(page));

        await Expect(page.GetByText("Username is required.").First).ToBeVisibleAsync();
        await Expect(ActiveStepHeading(page, "Account")).ToBeVisibleAsync();
        await Expect(page.Locator("#firstname")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task OnboardingRejectsMalformedReservedAndAlreadyTakenUsernames()
    {
        var taken = await app.CreatePersonaAsync("onboard-taken-name");
        var takenUsername = $"takenname{Guid.NewGuid():N}"[..18];
        using (var takenApi = app.CreateApiClient(taken))
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "v1/lan/users/me")
            {
                Content = JsonContent.Create(new
                {
                    username = takenUsername,
                    firstname = "Taken",
                    lastname = "Name"
                })
            };
            var update = await takenApi.SendAsync(request);
            update.EnsureSuccessStatusCode();
        }

        var persona = await app.CreatePersonaAsync("onboard-negative", createProfile: false);

        await using var context = await app.NewAuthenticatedContextAsync(persona);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}complete-profile");
        await page.WaitForInteractiveAsync();

        await page.FillAsync("#username", "ab");
        await page.ClickWhenInteractiveAsync(ContinueButton(page));
        await Expect(page.GetByText("Username must be 3-32 alphanumeric characters.").First).ToBeVisibleAsync();
        await Expect(ActiveStepHeading(page, "Account")).ToBeVisibleAsync();

        await page.FillAsync("#username", "admin");
        // Availability is checked on blur, so leave the field before asserting its response.
        await page.Locator("#username").PressAsync("Tab");
        await Expect(page.GetByText("Username is reserved.")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(ContinueButton(page));
        await Expect(ActiveStepHeading(page, "Account")).ToBeVisibleAsync();

        await page.FillAsync("#username", takenUsername);
        await page.Locator("#username").PressAsync("Tab");
        await Expect(page.GetByText("Username already exists.")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(ContinueButton(page));
        await Expect(ActiveStepHeading(page, "Account")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task OnboardingConfirmsUsernameAvailabilityBeforeAdvancing()
    {
        var persona = await app.CreatePersonaAsync("onboard-available", createProfile: false);
        var username = UniqueUsername("freeplayer");

        await using var context = await app.NewAuthenticatedContextAsync(persona);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}complete-profile");
        await page.WaitForInteractiveAsync();

        await page.FillAsync("#username", username);
        await page.Locator("#username").PressAsync("Tab");

        await Expect(page.GetByText("Username is available.")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(ContinueButton(page));
        await Expect(ActiveStepHeading(page, "About you")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task OnboardingValidatesRequiredNamesBeforeLeavingAboutStep()
    {
        var persona = await app.CreatePersonaAsync("onboard-names", createProfile: false);
        var username = UniqueUsername("namecheck");

        await using var context = await app.NewAuthenticatedContextAsync(persona);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}complete-profile");
        await page.WaitForInteractiveAsync();

        await page.FillAsync("#username", username);
        await page.ClickWhenInteractiveAsync(ContinueButton(page));
        await Expect(ActiveStepHeading(page, "About you")).ToBeVisibleAsync();

        // The identity provider supplies these OIDC name claims; clear them to exercise required validation.
        await page.FillAsync("#firstname", string.Empty);
        await page.FillAsync("#lastname", string.Empty);
        await page.ClickWhenInteractiveAsync(ContinueButton(page));

        await Expect(page.GetByText("First name is required.").First).ToBeVisibleAsync();
        await Expect(page.GetByText("Last name is required.").First).ToBeVisibleAsync();
        await Expect(ActiveStepHeading(page, "About you")).ToBeVisibleAsync();
        await Expect(page.Locator("#discordId")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task NewUserCompletesEveryOnboardingStepAndTheProfilePersists()
    {
        var persona = await app.CreatePersonaAsync("onboard-success", createProfile: false);
        var username = UniqueUsername("newplayer");

        await using var context = await app.NewAuthenticatedContextAsync(persona);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}complete-profile?returnUrl=%2Fprofile");
        await page.WaitForInteractiveAsync();

        await page.FillAsync("#username", username);
        await page.Locator("#username").PressAsync("Tab");
        await Expect(page.GetByText("Username is available.")).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(ContinueButton(page));

        await Expect(ActiveStepHeading(page, "About you")).ToBeVisibleAsync();
        await page.FillAsync("#firstname", "Nova");
        await page.FillAsync("#lastname", "Player");
        await page.ClickWhenInteractiveAsync(ContinueButton(page));

        await Expect(ActiveStepHeading(page, "Gaming profiles")).ToBeVisibleAsync();
        await page.FillAsync("#discordId", "nova#0001");
        await page.FillAsync("#steamId", "nova-steam");
        await page.FillAsync("#riotId", "Nova#EUW");
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Create profile" }));

        await Expect(page).ToHaveURLAsync(new Regex("/profile$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your information" })).ToBeVisibleAsync();
        await Expect(page.Locator("#username")).ToHaveValueAsync(username);
        await Expect(page.Locator("#firstname")).ToHaveValueAsync("Nova");
        await Expect(page.Locator("#discordId")).ToHaveValueAsync("nova#0001");

        await page.ReloadAsync();
        await Expect(page.Locator("#username")).ToHaveValueAsync(username);
        await Expect(page.Locator("#riotId")).ToHaveValueAsync("Nova#EUW");

        await using var db = app.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Auth0UserId == persona.Subject);
        Assert.Equal(username, user.Username);
        Assert.Equal("Nova", user.Firstname);
        Assert.Equal("Player", user.Lastname);
        Assert.Equal("nova#0001", user.DiscordId);
        Assert.True(user.IsComplete);
    }

    [Fact]
    public async Task RegistrationOnboardingCanBeCancelledAndSignsTheNewUserOut()
    {
        var persona = await app.CreatePersonaAsync("onboard-cancel", createProfile: false);

        await using var context = await app.NewAuthenticatedContextAsync(persona);
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}complete-profile?registration=true&returnUrl=%2F");
        await page.WaitForInteractiveAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Create your player profile" })).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }));

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();
        await Expect(page.Locator("#username")).ToHaveCountAsync(0);

        await using var db = app.CreateDbContext();
        Assert.False(await db.Users.AsNoTracking().AnyAsync(candidate => candidate.Auth0UserId == persona.Subject));
    }

    [Fact]
    public async Task ProfileSaveRequiresAValidUsernameAndDoesNotPersistInvalidEdits()
    {
        var member = await app.CreatePersonaAsync("profile-validate");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        await page.FillAsync("#username", string.Empty);
        await page.ClickWhenInteractiveAsync(SaveProfileButton(page));
        await Expect(page.GetByText("Username is required.").First).ToBeVisibleAsync();

        await page.FillAsync("#username", "xx");
        await page.ClickWhenInteractiveAsync(SaveProfileButton(page));
        await Expect(page.GetByText("Username must be 3-32 alphanumeric characters.").First).ToBeVisibleAsync();

        await using var db = app.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Auth0UserId == member.Subject);
        Assert.Equal(member.Username, user.Username);
    }

    [Fact]
    public async Task ProfileEditPersistsEveryFieldAndTheNewUsername()
    {
        var member = await app.CreatePersonaAsync("profile-edit");
        var newUsername = UniqueUsername("edited");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        await page.FillAsync("#username", newUsername);
        await page.FillAsync("#firstname", "Edited");
        await page.FillAsync("#lastname", "Player");
        await page.FillAsync("#discordId", "discord-e2e");
        await page.FillAsync("#steamId", "steam-e2e");
        await page.FillAsync("#riotId", "riot-e2e");
        await page.ClickWhenInteractiveAsync(SaveProfileButton(page));

        await Expect(page.GetByText("Profile saved.")).ToBeVisibleAsync();

        await page.ReloadAsync();
        await Expect(page.Locator("#username")).ToHaveValueAsync(newUsername);
        await Expect(page.Locator("#lastname")).ToHaveValueAsync("Player");
        await Expect(page.Locator("#steamId")).ToHaveValueAsync("steam-e2e");
        await Expect(page.Locator("#riotId")).ToHaveValueAsync("riot-e2e");
        await Expect(page.Locator($"#account-nav-menu-container button[aria-label*='{newUsername}']")).ToBeVisibleAsync();

        await using var db = app.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Auth0UserId == member.Subject);
        Assert.Equal(newUsername, user.Username);
        Assert.Equal(newUsername.ToLowerInvariant(), user.NormalizedUsername);
        Assert.Equal("discord-e2e", user.DiscordId);
        Assert.Equal("Edited", user.Firstname);
    }

    [Fact]
    public async Task UnverifiedProfileOffersResendVerificationAndReportsGenericOutcome()
    {
        var member = await app.CreatePersonaAsync("profile-unverified", emailVerified: false);

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        await Expect(page.GetByText("Unverified")).ToBeVisibleAsync();
        var resend = page.GetByRole(AriaRole.Button, new() { Name = "Resend verification email" });
        await Expect(resend).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(resend);

        await Expect(page.GetByText("If verification is available for this account, a verification email has been sent."))
            .ToBeVisibleAsync();
        await Expect(resend).ToBeEnabledAsync();
    }

    [Fact]
    public async Task ProfilePasswordResetReportsGenericOutcome()
    {
        var member = await app.CreatePersonaAsync("profile-password-reset", hasPasswordResetIdentity: true);

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Reset password" }));

        await Expect(page.GetByText("If password reset is available for this account, a password reset email has been sent."))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task DeleteAccountStaysDisabledUntilTheExactUsernameIsTyped()
    {
        var member = await app.CreatePersonaAsync("profile-delete-guard");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        var deleteButton = page.GetByRole(AriaRole.Button, new() { Name = "Delete account" });
        await Expect(page.Locator("#deleteConfirmHelp")).ToContainTextAsync(member.Username);
        await Expect(deleteButton).ToBeDisabledAsync();

        await page.FillAsync("#deleteConfirm", "definitely-not-my-name");
        await page.Locator("#deleteConfirm").PressAsync("Tab");
        await Expect(deleteButton).ToBeDisabledAsync();

        await page.FillAsync("#deleteConfirm", member.Username);
        // InputText commits on blur before the delete guard can re-render.
        await page.Locator("#deleteConfirm").PressAsync("Tab");
        await Expect(deleteButton).ToBeEnabledAsync();
    }

    [Fact]
    public async Task AbandoningDeleteConfirmationLeavesTheAccountIntact()
    {
        var member = await app.CreatePersonaAsync("profile-delete-abandon");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        await page.FillAsync("#deleteConfirm", member.Username + "-typo");
        await page.Locator("#deleteConfirm").PressAsync("Tab");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Delete account" })).ToBeDisabledAsync();
        await page.FillAsync("#deleteConfirm", string.Empty);

        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your information" })).ToBeVisibleAsync();
        await Expect(page.Locator("#username")).ToHaveValueAsync(member.Username);

        await using var db = app.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Auth0UserId == member.Subject);
        Assert.False(user.IsDeleted);
        Assert.Equal(member.Username, user.Username);
    }

    [Fact]
    public async Task DeleteAccountAnonymizesTheProfileAndEndsTheSession()
    {
        var member = await app.CreatePersonaAsync("profile-delete");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        await page.FillAsync("#deleteConfirm", member.Username);
        await page.Locator("#deleteConfirm").PressAsync("Tab");
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Delete account" }));

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();

        await using (var db = app.CreateDbContext())
        {
            var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Auth0UserId == member.Subject);
            Assert.True(user.IsDeleted);
            Assert.NotNull(user.DeletedAtUtc);
            Assert.Null(user.Firstname);
            Assert.Null(user.Lastname);
            Assert.Null(user.Email);
            Assert.StartsWith("deleted-user-", user.Username);
        }

        // Signing the anonymized identity back in must not resurrect a usable profile: the API
        // reports the account as gone and the app drops the session instead of showing /profile.
        await page.GotoAsync($"{app.BaseUrl}");
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Log in" }));
        await app.SelectPersonaAsync(page, member);

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();
        Assert.DoesNotContain("/profile", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfileLoadFailureShowsUnavailableStateAndRecovers()
    {
        var member = await app.CreatePersonaAsync("profile-load-fault");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();

        await using (var fault = await DatabaseReadFault.InstallAsync(app, "users"))
        {
            await page.GotoAsync($"{app.BaseUrl}profile");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Profile unavailable" }))
                .ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Try again" }))
                .ToHaveAttributeAsync("href", "/profile");
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Back Home" })).ToBeVisibleAsync();
        }

        // The StatusPage retry is a link back to /profile, so the recovery is a real reload with the
        // identity table available again.
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Link, new() { Name = "Try again" }));

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your information" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(page.Locator("#username")).ToHaveValueAsync(member.Username);
    }

    private static ILocator ContinueButton(IPage page) =>
        page.GetByRole(AriaRole.Button, new() { Name = "Continue" });

    private static ILocator SaveProfileButton(IPage page) =>
        page.GetByRole(AriaRole.Button, new() { Name = "Save profile" });

    private static ILocator ActiveStepHeading(IPage page, string stepTitle) =>
        page.GetByRole(AriaRole.Heading, new() { Name = stepTitle, Level = 2 });

    private async Task OpenProfileAsync(IPage page)
    {
        await page.GotoAsync($"{app.BaseUrl}profile");
        await page.WaitForInteractiveAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your information" })).ToBeVisibleAsync();
        await Expect(page.Locator("#username")).ToHaveValueAsync(new Regex("."));
    }

    private static string UniqueUsername(string prefix) =>
        prefix + Guid.NewGuid().ToString("N")[..6];
}
