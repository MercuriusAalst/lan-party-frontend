using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Shared preconditions and locators for the team/multi-user lane.
/// Product behaviour always goes through the browser; these helpers only build starting state
/// through the real API (plus one targeted timestamp write) so tests stay short and independent.
/// </summary>
internal static class TeamE2E
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal const string GenericActionFailure = "The team action could not be completed right now.";

    // 1x1 transparent PNG.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    internal static byte[] OnePixelPngBytes() => Convert.FromBase64String(OnePixelPngBase64);

    internal static string UniqueLabel(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    internal static string UniqueTeamName() => $"E2E Team {Guid.NewGuid().ToString("N")[..8]}";

    internal static Guid RequireUserId(E2EPersona persona) =>
        persona.UserId ?? throw new InvalidOperationException($"Persona '{persona.Username}' has no profile row; create it with createProfile: true.");

    // ---------- API preconditions ----------

    internal static async Task<TeamManagementSummary> CreateTeamAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/v1/lan/teams", new { name }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamManagementSummary>(Json))!;
    }

    internal static async Task<CurrentUserTeamSummary> GetSummaryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/v1/lan/teams/me/summary");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CurrentUserTeamSummary>(Json))!;
    }

    internal static async Task<TeamInviteView> InviteAsync(HttpClient client, Guid teamId, Guid userId)
    {
        var response = await client.PostAsJsonAsync($"/v1/lan/teams/{teamId}/invites", new { userId }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamInviteView>(Json))!;
    }

    internal static async Task<TeamInviteView> RespondToInviteAsync(HttpClient client, Guid inviteId, bool accept)
    {
        var response = await client.PatchAsJsonAsync($"/v1/lan/team-invites/{inviteId}", new { accept }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamInviteView>(Json))!;
    }

    /// <summary>Invite then accept, so the user becomes a real team member.</summary>
    internal static async Task AddMemberAsync(HttpClient captainClient, HttpClient memberClient, Guid teamId, Guid memberUserId)
    {
        var invite = await InviteAsync(captainClient, teamId, memberUserId);
        await RespondToInviteAsync(memberClient, invite.Id, accept: true);
    }

    /// <summary>Invite then decline, which starts the resend-cooldown counting for the target user.</summary>
    internal static async Task DeclineInviteAsync(HttpClient captainClient, HttpClient memberClient, Guid teamId, Guid memberUserId)
    {
        var invite = await InviteAsync(captainClient, teamId, memberUserId);
        await RespondToInviteAsync(memberClient, invite.Id, accept: false);
    }

    /// <summary>Moves a pending invite into the past so the Expired state is reachable without waiting.</summary>
    internal static async Task ExpireInviteAsync(PlaywrightE2EFixture app, Guid inviteId)
    {
        await using var db = app.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE teams.team_invites SET \"ExpiresAt\" = now() - interval '1 day' WHERE \"Id\" = {0}",
            inviteId);
    }

    /// <summary>
    /// Cancels an invite behind the app's back so no realtime event reaches the invitee.
    /// Used to observe the stale-invite action failure in the notification menu.
    /// </summary>
    internal static async Task CancelInviteWithoutEventAsync(PlaywrightE2EFixture app, Guid inviteId)
    {
        await using var db = app.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE teams.team_invites SET \"Status\" = 3, \"CancelledAt\" = now() WHERE \"Id\" = {0}",
            inviteId);
    }

    /// <summary>Reads the persisted invite status straight from the isolated database.</summary>
    internal static async Task<string> ReadInviteStatusAsync(PlaywrightE2EFixture app, Guid inviteId)
    {
        await using var db = app.CreateDbContext();
        var status = await db.Database
            .SqlQueryRaw<int>("SELECT \"Status\" AS \"Value\" FROM teams.team_invites WHERE \"Id\" = {0}", inviteId)
            .SingleAsync();

        return status switch
        {
            0 => "Pending",
            1 => "Accepted",
            2 => "Declined",
            3 => "Cancelled",
            4 => "Expired",
            _ => $"Unknown({status})"
        };
    }

    internal static async Task<int> CountInvitesAsync(PlaywrightE2EFixture app, Guid teamId, Guid userId)
    {
        await using var db = app.CreateDbContext();
        return await db.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*)::int AS \"Value\" FROM teams.team_invites WHERE \"TeamId\" = {0} AND \"UserId\" = {1}",
                teamId,
                userId)
            .SingleAsync();
    }

    // ---------- Browser ----------

    internal static async Task<IPage> LoginAndOpenAsync(
        PlaywrightE2EFixture app,
        TeamContext context,
        E2EPersona persona,
        string relativePath)
    {
        var page = await context.NewPageAsync();
        await app.LoginAsync(page, persona);
        await page.GotoAsync(new Uri(new Uri(app.BaseUrl), relativePath).ToString());
        await page.WaitForInteractiveAsync();
        return page;
    }

    internal static async Task<IPage> OpenAnonymousAsync(
        PlaywrightE2EFixture app,
        TeamContext context,
        string relativePath)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(app.BaseUrl), relativePath).ToString());
        await page.WaitForInteractiveAsync();
        return page;
    }

    internal static string TeamProfilePath(string teamName) => $"/teams/{Uri.EscapeDataString(teamName)}";

    internal static ILocator TeamSelector(IPage page) =>
        page.GetByRole(AriaRole.Complementary, new() { Name = "Your teams" });

    internal static ILocator TeamSelectorItem(IPage page, string teamName) =>
        TeamSelector(page).GetByRole(AriaRole.Button, new() { Name = teamName });

    internal static ILocator MemberCard(IPage page, string username) =>
        page.GetByRole(AriaRole.Button, new() { Name = $"View participant details for {username}" });

    internal static ILocator ConfirmationDialog(IPage page, string title) =>
        page.GetByRole(AriaRole.Dialog, new() { Name = title });

    internal static ILocator TeamToolsTab(IPage page) =>
        page.GetByRole(AriaRole.Tab, new() { Name = "Team tools" });

    internal static ILocator MembersTab(IPage page) =>
        page.GetByRole(AriaRole.Tab, new() { Name = "Members" });

    internal static ILocator InvitePanelButton(IPage page) =>
        page.GetByRole(AriaRole.Button, new() { Name = "Invite player", Exact = true }).First;

    internal static ILocator InviteDialog(IPage page) =>
        page.GetByRole(AriaRole.Dialog, new() { Name = "Invite player", Exact = true });

    internal static ILocator CreateTeamDialog(IPage page) =>
        page.GetByRole(AriaRole.Dialog, new() { Name = "Create team", Exact = true });

    internal static ILocator SentInvites(IPage page) => page.Locator(".team-sent-invite-list li");

    /// <summary>Drives the captain's invite dialog end to end (open, search, select, send).</summary>
    internal static async Task SendInviteThroughUiAsync(IPage page, string inviteeUsername)
    {
        await ClickAsync(page, InvitePanelButton(page));
        var dialog = InviteDialog(page);
        await dialog.GetByLabel("Search username").FillAsync(inviteeUsername);
        var candidate = dialog.GetByRole(AriaRole.Option, new() { Name = inviteeUsername });
        await candidate.WaitForAsync();
        await ClickAsync(page, candidate);
        await ClickAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Send invite", Exact = true }));
        await Expect(dialog).Not.ToBeVisibleAsync();
    }

    private static Task ClickAsync(IPage page, ILocator target) => page.ClickWhenInteractiveAsync(target);

    /// <summary>
    /// Opens an authenticated page, waiting for team management's current groups when requested.
    /// The hub is server-side, so the browser cannot observe its connection directly.
    /// </summary>
    internal static async Task<IPage> LoginAndOpenWithLiveUpdatesAsync(
        PlaywrightE2EFixture app,
        TeamContext context,
        E2EPersona persona,
        string relativePath)
    {
        var page = await LoginAndOpenAsync(app, context, persona, relativePath);
        await Expect(page.GetByText("Live updates are unavailable. Your changes will still appear after each action."))
            .Not.ToBeVisibleAsync();
        if(string.Equals(relativePath, "/teams/manage", StringComparison.OrdinalIgnoreCase))
        {
            await Expect(page.Locator("#team-workspace"))
                .ToHaveAttributeAsync("data-live-updates-ready", "true");
        }

        return page;
    }

    /// <summary>
    /// Wraps a fixture browser context so disposal stops tracing and closes the context through the
    /// fixture (which keeps the trace artifacts usable when a test fails).
    /// </summary>
    internal sealed class TeamContext : IAsyncDisposable
    {
        private readonly PlaywrightE2EFixture _app;
        private readonly IBrowserContext _context;

        private TeamContext(PlaywrightE2EFixture app, IBrowserContext context)
        {
            _app = app;
            _context = context;
        }

        internal static async Task<TeamContext> CreateAsync(PlaywrightE2EFixture app) =>
            new(app, await app.NewContextAsync());

        internal Task<IPage> NewPageAsync() => _context.NewPageAsync();

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _app.CloseContextAsync(_context);
            }
            catch (PlaywrightException)
            {
            }
        }
    }

    // ---------- DTOs (mirror the API JSON contract) ----------

    internal sealed record TeamPublicUser(Guid Id, string? Username, string? DisplayName);

    internal sealed record TeamManagementSummary(
        Guid Id,
        string Name,
        Guid CaptainUserId,
        string? CaptainUsername,
        string? LogoUrl,
        List<TeamPublicUser> Members);

    internal sealed record TeamInviteView(
        Guid Id,
        Guid TeamId,
        Guid UserId,
        string? Status,
        DateTime CreatedAt,
        DateTime ExpiresAt);

    internal sealed record TeamInviteSummary(
        Guid Id,
        Guid TeamId,
        string TeamName,
        Guid UserId,
        string? Username,
        string? Status,
        DateTime CreatedAt,
        DateTime ExpiresAt);

    internal sealed record CurrentUserTeamSummary(
        List<TeamManagementSummary> CaptainedTeams,
        List<TeamManagementSummary> MemberTeams,
        List<TeamInviteSummary> ReceivedPendingInvites,
        List<TeamInviteSummary> SentPendingInvites);

    internal sealed class NavigationCounter : IDisposable
    {
        private readonly IPage _page;
        private int _count;

        internal NavigationCounter(IPage page)
        {
            _page = page;
            page.FrameNavigated += OnFrameNavigated;
        }

        internal int Count => Volatile.Read(ref _count);

        private void OnFrameNavigated(object? sender, IFrame frame) => Interlocked.Increment(ref _count);

        public void Dispose() => _page.FrameNavigated -= OnFrameNavigated;
    }
}
