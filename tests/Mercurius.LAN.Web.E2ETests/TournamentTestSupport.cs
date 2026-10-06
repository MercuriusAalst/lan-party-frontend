using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Shared, thin helpers for the tournament / registration / leaderboard / sponsor suites.
/// Preconditions are created through the real API so the browser only exercises the
/// behaviour under test.
/// </summary>
internal static class TournamentE2E
{
    // Valid 1x1 PNG. The API's media pipeline decodes uploads with Imageflow and this exact literal
    // was already proven to decode (the sponsor suite created, edited and deleted sponsors past it),
    // so the fixture reuses it instead of generating or embedding a larger image.
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>
    /// Navigates to an application route. Playwright's auto-waiting assertions/actions handle the
    /// SSR then Blazor circuit handoff, so no explicit circuit wait is needed here.
    /// </summary>
    public static Task GotoInteractiveAsync(IPage page, string path) => page.GotoAsync(path);

    public static Task ReloadInteractiveAsync(IPage page) => page.ReloadAsync();

    /// <summary>
    /// Paged result sets are server-side; walk forward until the card for <paramref name="name"/>
    /// is on the loaded page so list assertions never depend on which page holds the data.
    /// </summary>
    public static async Task EnsureCardVisibleAsync(IPage page, string name, int maxPages = 15)
    {
        var card = page.Locator("article.tournaments-card").Filter(new() { HasText = name });
        for (var i = 0; i < maxPages; i++)
        {
            if (await card.CountAsync() > 0)
                return;

            var next = page.Locator("nav.tournaments-pagination").GetByRole(AriaRole.Button, new() { Name = "Next" });
            if (await next.CountAsync() == 0 || !await next.IsEnabledAsync())
                return;

            await next.ClickAsync();
        }
    }

    public static string TestImagePath()
    {
        var path = Path.Combine(Path.GetTempPath(), "mercurius-e2e-pixel.png");
        if (!File.Exists(path))
            File.WriteAllBytes(path, PngBytes);
        return path;
    }

    public static MultipartFormDataContent TournamentForm(
        string name,
        string bracketType,
        string participationMode,
        string format = "BestOf1",
        string finalsFormat = "BestOf1",
        int? teamSize = null,
        string? rankingMetric = null,
        DateTime? plannedStart = null,
        int averageGameDurationMinutes = 30,
        int roundBreakDurationMinutes = 10)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "Name" },
            { new StringContent(bracketType), "BracketType" },
            { new StringContent(participationMode), "ParticipationMode" },
            { new StringContent(format), "Format" },
            { new StringContent(finalsFormat), "FinalsFormat" },
            // The API's DateTimeExtensions.EnsureUtc intentionally rejects timezone-less values, so the
            // precondition must send an explicit UTC instant - the same thing the browser form sends.
            {
                new StringContent(
                    (plannedStart ?? DateTime.UtcNow.AddDays(-1)).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss'Z'")),
                "PlannedStartTime"
            },
            { new StringContent(averageGameDurationMinutes.ToString()), "AverageGameDurationMinutes" },
            { new StringContent(roundBreakDurationMinutes.ToString()), "RoundBreakDurationMinutes" }
        };
        if (teamSize.HasValue)
            form.Add(new StringContent(teamSize.Value.ToString()), "TeamSize");
        if (!string.IsNullOrWhiteSpace(rankingMetric))
            form.Add(new StringContent(rankingMetric), "LeaderboardRankingMetric");

        var image = new ByteArrayContent(PngBytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "Image", "banner.png");
        return form;
    }

    public static MultipartFormDataContent SponsorForm(
        string name,
        string tier,
        string infoUrl,
        string? description = null,
        bool includeLogo = true)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "Name" },
            { new StringContent(tier), "SponsorTier" },
            { new StringContent(infoUrl), "InfoUrl" }
        };
        if (description is not null)
            form.Add(new StringContent(description), "Description");
        if (includeLogo)
        {
            var logo = new ByteArrayContent(PngBytes);
            logo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(logo, "Logo", "logo.png");
        }

        return form;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"API {(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {body}");

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        return document.RootElement.Clone();
    }

    public static Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    public static async Task<Guid> CreateTournamentAsync(
        HttpClient admin,
        string name,
        string bracketType,
        string participationMode,
        string format = "BestOf1",
        string finalsFormat = "BestOf1",
        int? teamSize = null,
        string? rankingMetric = null)
    {
        using var form = TournamentForm(name, bracketType, participationMode, format, finalsFormat, teamSize, rankingMetric);
        var response = await admin.PostAsync("v1/lan/tournaments", form);
        var json = await ReadJsonAsync(response);
        return json.GetProperty("id").GetGuid();
    }

    public static Task<Guid> CreateScheduledIndividualTournamentAsync(HttpClient admin, string name) =>
        CreateTournamentAsync(admin, name, "SingleElimination", "Individual");

    public static Task<Guid> CreateScheduledTeamTournamentAsync(HttpClient admin, string name, int teamSize) =>
        CreateTournamentAsync(admin, name, "SingleElimination", "Team", teamSize: teamSize);

    public static Task<Guid> CreateLeaderboardTournamentAsync(HttpClient admin, string name, string metric) =>
        CreateTournamentAsync(admin, name, "Leaderboard", "Individual", rankingMetric: metric);

    public static async Task SetLifecycleAsync(HttpClient admin, Guid tournamentId, string state)
    {
        var response = await SendJsonAsync(
            admin, HttpMethod.Put, $"v1/lan/tournaments/{tournamentId}/lifecycle-state", new { state });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Lifecycle {state} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    public static async Task RegisterIndividualAsync(HttpClient client, Guid tournamentId)
    {
        var response = await SendJsonAsync(
            client, HttpMethod.Put, $"v1/lan/tournaments/{tournamentId}/registrations/individual/me");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Individual registration failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    public static async Task<Guid> CreateTeamAsync(HttpClient captain, Guid captainUserId, string name)
    {
        var response = await SendJsonAsync(captain, HttpMethod.Post, "v1/lan/teams", new { name, captainUserId });
        var json = await ReadJsonAsync(response);
        return json.GetProperty("id").GetGuid();
    }

    public static async Task InviteAndAcceptAsync(HttpClient captain, HttpClient member, Guid teamId, Guid memberUserId)
    {
        var inviteResponse = await SendJsonAsync(
            captain, HttpMethod.Post, $"v1/lan/teams/{teamId}/invites", new { userId = memberUserId });
        var invite = await ReadJsonAsync(inviteResponse);
        var inviteId = invite.GetProperty("id").GetGuid();
        var acceptResponse = await SendJsonAsync(
            member, HttpMethod.Patch, $"v1/lan/team-invites/{inviteId}", new { accept = true });
        if (!acceptResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Accept invite failed: {(int)acceptResponse.StatusCode} {await acceptResponse.Content.ReadAsStringAsync()}");
    }

    public static async Task SubmitTeamRosterAsync(HttpClient captain, Guid tournamentId, Guid teamId, params Guid[] userIds)
    {
        var response = await SendJsonAsync(
            captain,
            HttpMethod.Put,
            $"v1/lan/tournaments/{tournamentId}/registrations/teams/{teamId}/roster",
            new { teamId, userIds });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Team roster failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    public static async Task RecordGuestAttemptAsync(
        HttpClient admin,
        Guid tournamentId,
        string guestName,
        decimal? score = null,
        long? durationMilliseconds = null)
    {
        var response = await SendJsonAsync(
            admin,
            HttpMethod.Post,
            $"v1/lan/tournaments/{tournamentId}/leaderboard/attempts",
            new { guestDisplayName = guestName, score, durationMilliseconds });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Recording leaderboard attempt failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    public static async Task<int> CreateSponsorAsync(
        HttpClient admin,
        string name,
        string tier,
        string infoUrl,
        string? description = null)
    {
        using var form = SponsorForm(name, tier, infoUrl, description);
        var response = await admin.PostAsync("v1/lan/sponsors", form);
        var json = await ReadJsonAsync(response);
        return json.GetProperty("id").GetInt32();
    }

    /// <summary>Locates a tournament by unique name so a UI-created row can be revisited by URL.</summary>
    public static async Task<Guid?> FindTournamentIdAsync(HttpClient admin, string name)
    {
        for (var page = 1; page <= 20; page++)
        {
            var response = await admin.GetAsync($"v1/lan/tournaments?page={page}&pageSize=100");
            var json = await ReadJsonAsync(response);
            foreach (var item in json.EnumerateArray())
            {
                if (string.Equals(item.GetProperty("name").GetString(), name, StringComparison.Ordinal))
                    return item.GetProperty("id").GetGuid();
            }

            if (json.GetArrayLength() < 100)
                return null;
        }

        return null;
    }

    /// <summary>English UI strings used by these suites (default culture is en-US).</summary>
    internal static class Text
    {
        public const string AddTournament = "Add tournament";
        public const string CreateTournament = "Create tournament";
        public const string Start = "Start";
        public const string Finish = "Finish";
        public const string Reset = "Reset";
        public const string Cancel = "Cancel";
        public const string Delete = "Delete";
        public const string Save = "Save";
        public const string Edit = "Edit";
        public const string SearchTournaments = "Search tournaments";
        public const string SortTournaments = "Sort tournaments";
        public const string NoFilterMatches = "No tournaments match your filters.";
        public const string StatusOpen = "Open";
        public const string StatusOngoing = "Ongoing";
        public const string StatusFinished = "Finished";
        public const string StatusCancelled = "Cancelled";
        public const string RegistrationClosed = "Registration closed";
        public const string StartRequiresRegistrations =
            "At least two active registrations in this tournament's participation mode are required to start.";
        public const string DeleteInProgress = "An in-progress tournament cannot be deleted. Finish or cancel it first.";
        public const string LeaderboardResultsRequired = "Record at least one valid leaderboard result before finishing.";
        public const string NotFoundTitle = "Tournament not found";
        public const string NotFoundText = "The tournament may have been removed or is not available.";
        public const string CreateSponsor = "Create sponsor";
        public const string UpdateSponsor = "Update sponsor";
        public const string DeleteSponsor = "Delete sponsor";
        public const string Clear = "Clear";
        public const string RecordResult = "Add entry";
        public const string LeaderboardNoResults = "No results yet";
        public const string ConfirmRegistration = "Confirm your spot";
        public const string Register = "Register";
        public const string JoinTournament = "Join this tournament";
        public const string AcceptRosterPlace = "Accept place";
        public const string DeclineRosterPlace = "Decline place";
        public const string RosterInvitation = "Roster invitation";
        public const string YouWereSelected = "You've been selected";
        public const string SignInToRegister = "Sign in to register";
        public const string RegistrationClosedNotScheduled =
            "Registration is closed because this tournament is no longer scheduled.";
        public const string EntryUnavailableScheduled =
            "Entries can be added once the tournament is in progress. Start the tournament first.";
        public const string TeamsYouCaptain = "Teams you captain";
        public const string TryAgain = "Try again";
        public const string LoadError = "The tournament could not be loaded.";
        public const string TeamsUnavailable = "Your teams could not be loaded.";
        public const string RosterEligibilityUnavailable = "Roster eligibility could not be checked.";
        public const string AccessDenied = "You do not have permission to access this page.";
    }
}
