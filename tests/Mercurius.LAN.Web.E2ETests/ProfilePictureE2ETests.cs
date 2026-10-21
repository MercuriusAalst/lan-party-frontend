using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Own profile picture lifecycle: local preview, confirmed upload, replacement, removal and the
/// shared header identity that must follow a confirmed change.
/// </summary>
[Collection(E2ECollection.Name)]
public class ProfilePictureE2ETests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    private const string PictureEditorSelector = ".profile-picture-editor";

    // A 1x1 PNG: small enough to be an accepted image and decodable by the media pipeline.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task ProfilePictureCanBePreviewedUploadedReplacedAndRemoved()
    {
        var member = await app.CreatePersonaAsync("profile-picture");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        var avatar = page.Locator($"{PictureEditorSelector} .user-avatar");
        await Expect(avatar.Locator("img")).ToHaveCountAsync(0);
        Assert.False(string.IsNullOrWhiteSpace(await avatar.InnerTextAsync()));

        await SelectPictureAsync(page, "avatar.png", TinyPng);
        await Expect(page.Locator(".profile-picture-file-name")).ToHaveTextAsync("avatar.png");
        await Expect(avatar.Locator("img")).ToHaveCountAsync(1);
        Assert.StartsWith("data:image/png;base64,", await avatar.Locator("img").GetAttributeAsync("src"));

        await using (var beforeUpload = app.CreateDbContext())
        {
            var pendingUser = await beforeUpload.Users.AsNoTracking()
                .SingleAsync(candidate => candidate.Auth0UserId == member.Subject);
            Assert.Null(pendingUser.ProfilePictureUrl);
        }

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Save picture" }));
        await Expect(page.Locator(".profile-picture-status")).ToHaveTextAsync("Profile picture updated.");

        var uploadedUrl = await avatar.Locator("img").GetAttributeAsync("src");
        Assert.NotNull(uploadedUrl);
        Assert.Matches(new Regex(@"^https?://[^/]+/images/[0-9a-f]{32}\.webp$", RegexOptions.IgnoreCase), uploadedUrl);

        // A confirmed change reaches the shared signed-in identity without a manual reload.
        await Expect(page.Locator("#account-nav-menu-container .user-avatar img")).ToHaveCountAsync(1);

        using(var api = app.CreateApiClient(member))
        {
            var team = await TeamE2E.CreateTeamAsync(api, TeamE2E.UniqueTeamName());
            Assert.Equal(new Uri(uploadedUrl!).AbsolutePath.TrimStart('/'), team.Members.Single().ProfilePictureUrl);
        }

        await page.GotoAsync($"{app.BaseUrl}teams/manage");
        await page.WaitForInteractiveAsync();
        await Expect(page.Locator(".team-member-row .user-avatar img")).ToHaveAttributeAsync("src", uploadedUrl);

        await page.GotoAsync($"{app.BaseUrl}profile");
        await page.WaitForInteractiveAsync();

        await SelectPictureAsync(page, "replacement.png", TinyPng);
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Save picture" }));
        await Expect(page.Locator(".profile-picture-status")).ToHaveTextAsync("Profile picture updated.");

        var replacedUrl = await avatar.Locator("img").GetAttributeAsync("src");
        Assert.NotNull(replacedUrl);
        Assert.NotEqual(uploadedUrl, replacedUrl);

        await page.ReloadAsync();
        await Expect(page.Locator($"{PictureEditorSelector} .user-avatar img")).ToHaveAttributeAsync("src", replacedUrl);

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Remove picture" }));
        await Expect(page.Locator(".profile-picture-status")).ToHaveTextAsync("Profile picture removed.");
        await Expect(page.Locator($"{PictureEditorSelector} .user-avatar img")).ToHaveCountAsync(0);

        await using var db = app.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Auth0UserId == member.Subject);
        Assert.Null(user.ProfilePictureUrl);
    }

    [Fact]
    public async Task OversizeProfilePictureIsRejectedLocallyWithoutUploading()
    {
        var member = await app.CreatePersonaAsync("profile-picture-limit");

        await using var context = await app.NewAuthenticatedContextAsync(member);
        var page = await context.NewPageAsync();
        await OpenProfileAsync(page);

        await SelectPictureAsync(page, "huge.png", new byte[(6 * 1024 * 1024)]);

        await Expect(page.Locator(".profile-picture-status")).ToContainTextAsync(new Regex("MB limit"));
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save picture" })).ToBeDisabledAsync();
        await Expect(page.Locator($"{PictureEditorSelector} .user-avatar img")).ToHaveCountAsync(0);

        await using var db = app.CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Auth0UserId == member.Subject);
        Assert.Null(user.ProfilePictureUrl);
    }

    private static Task SelectPictureAsync(IPage page, string fileName, byte[] content) =>
        page.SetInputFilesAsync($"{PictureEditorSelector} input[type=file]", new FilePayload
        {
            Name = fileName,
            MimeType = "image/png",
            Buffer = content
        });

    private async Task OpenProfileAsync(IPage page)
    {
        await page.GotoAsync($"{app.BaseUrl}profile");
        await page.WaitForInteractiveAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your information" })).ToBeVisibleAsync();
        await Expect(page.Locator(PictureEditorSelector)).ToBeVisibleAsync();
    }
}
