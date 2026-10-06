using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Preconditions and locators for the tournament match / scoring / bracket lane.
/// Product behaviour always runs through the browser; these helpers only build the starting
/// state through the real API, plus one targeted timestamp write because the score
/// confirmation and correction windows are real five-minute clocks.
/// </summary>
internal static class MatchE2E
{
    // Rendered labels from TournamentMatchDetailsDialog (en-US).
    internal const string AwaitingConfirmation = "Awaiting confirmation";
    internal const string ReadyForScore = "Ready for score";
    internal const string ScoreConfirmationStatus = "Score confirmation";
    internal const string DisputedStatus = "Score disputed";
    internal const string AdminResolutionStatus = "Admin resolution required";
    internal const string CompletedStatus = "Completed";
    internal const string ForfeitedStatus = "Forfeited";
    internal const string ReversedStatus = "Reversed";

    internal const string ConfirmEndedButton = "Confirm match ended";
    internal const string SubmitScoreButton = "Submit score report";
    internal const string ForfeitSideButton = "Forfeit your side";

    internal const string YourActionsPanel = "Your match actions";
    internal const string AdminPanel = "Administrator match actions";
    internal const string DeadlineLabel = "Score window deadline";

    internal const string ActionSaveFailed = "The match action could not be saved. Try again.";
    internal const string ScoreReportSaved = "Your score report was saved.";
    internal const string TournamentNotInProgress = "This tournament is no longer in progress.";
    internal const string MatchNotDisputed = "This match does not currently require administrator resolution.";
    internal const string NoOfficialResultToReverse = "This match does not have an official result to reverse.";
    internal const string EnterScoresBeforeResolve = "Enter both participant scores before resolving this match.";

    internal static string UniqueLabel(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>Creates unrelated personas whose usernames are unique per test run.</summary>
    internal static async Task<IReadOnlyList<E2EPersona>> CreatePersonasAsync(
        PlaywrightE2EFixture app,
        string prefix,
        int count)
    {
        var personas = new List<E2EPersona>();
        for (var index = 0; index < count; index++)
            personas.Add(await app.CreatePersonaAsync(UniqueLabel($"{prefix}{index + 1}")));
        return personas;
    }

    // ---------- tournament preconditions (real API) ----------

    internal static async Task<Guid> StartIndividualTournamentAsync(
        PlaywrightE2EFixture app,
        HttpClient admin,
        IReadOnlyList<E2EPersona> participants,
        string bracketType = "SingleElimination",
        string format = "BestOf1",
        string? finalsFormat = null)
    {
        var tournamentId = await TournamentE2E.CreateTournamentAsync(
            admin,
            TournamentE2E.Unique($"E2E {bracketType} bracket"),
            bracketType,
            "Individual",
            format,
            finalsFormat ?? format);

        foreach (var participant in participants)
            await TournamentE2E.RegisterIndividualAsync(app.CreateApiClient(participant), tournamentId);

        await TournamentE2E.SetLifecycleAsync(admin, tournamentId, "InProgress");
        return tournamentId;
    }

    internal static async Task<Guid> StartTeamTournamentAsync(
        PlaywrightE2EFixture app,
        HttpClient admin,
        IReadOnlyList<(E2EPersona Captain, string TeamName)> teams,
        string format = "BestOf1",
        int teamSize = 1)
    {
        var tournamentId = await TournamentE2E.CreateTournamentAsync(
            admin,
            TournamentE2E.Unique("E2E team bracket"),
            "SingleElimination",
            "Team",
            format,
            format,
            teamSize: teamSize);

        foreach (var (captain, teamName) in teams)
        {
            var client = app.CreateApiClient(captain);
            var team = await TeamE2E.CreateTeamAsync(client, teamName);
            await TournamentE2E.SubmitTeamRosterAsync(client, tournamentId, team.Id, TeamE2E.RequireUserId(captain));
        }

        await TournamentE2E.SetLifecycleAsync(admin, tournamentId, "InProgress");
        return tournamentId;
    }

    internal static async Task<IReadOnlyList<MatchView>> GetMatchesAsync(HttpClient client, Guid tournamentId)
    {
        var tournament = await TournamentE2E.ReadJsonAsync(await client.GetAsync($"v1/lan/tournaments/{tournamentId}"));
        return tournament.GetProperty("matches").EnumerateArray().Select(MatchView.From).ToList();
    }

    /// <summary>Fast preconditions: drive the participant confirm/score endpoints without the browser.</summary>
    internal static async Task ConfirmEndedViaApiAsync(PlaywrightE2EFixture app, Guid matchId, E2EPersona persona)
    {
        var response = await app.CreateApiClient(persona).PostAsync($"v1/lan/matches/{matchId}/confirm-ended", null);
        response.EnsureSuccessStatusCode();
    }

    internal static async Task SubmitScoreViaApiAsync(
        PlaywrightE2EFixture app,
        Guid matchId,
        E2EPersona persona,
        int participant1Score,
        int participant2Score)
    {
        var response = await TournamentE2E.SendJsonAsync(
            app.CreateApiClient(persona),
            HttpMethod.Put,
            $"v1/lan/matches/{matchId}/score",
            new { Participant1Score = participant1Score, Participant2Score = participant2Score });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Both sides confirm the end, which is the state every score report starts from.</summary>
    internal static async Task ReachReadyForScoreAsync(
        PlaywrightE2EFixture app,
        Guid tournamentId,
        E2EPersona first,
        E2EPersona second)
    {
        var match = (await GetMatchesAsync(app.Api, tournamentId)).Single();
        await ConfirmEndedViaApiAsync(app, match.Id, first);
        await ConfirmEndedViaApiAsync(app, match.Id, second);
    }

    /// <summary>Completes a match through the API, keeping bracket/progression tests focused.</summary>
    internal static async Task CompleteMatchViaApiAsync(
        PlaywrightE2EFixture app,
        Guid matchId,
        E2EPersona first,
        E2EPersona second,
        int participant1Score,
        int participant2Score)
    {
        await ConfirmEndedViaApiAsync(app, matchId, first);
        await ConfirmEndedViaApiAsync(app, matchId, second);
        await SubmitScoreViaApiAsync(app, matchId, first, participant1Score, participant2Score);
        await SubmitScoreViaApiAsync(app, matchId, second, participant1Score, participant2Score);
    }

    internal static E2EPersona PersonaForUser(IReadOnlyList<E2EPersona> personas, Guid? userId) =>
        personas.Single(persona => persona.UserId == userId);

    /// <summary>
    /// Moves the score confirmation (or correction) deadline into the past so the timeout
    /// transition can be observed without waiting five real minutes.
    /// </summary>
    internal static async Task ExpireScoreWindowAsync(PlaywrightE2EFixture app, Guid matchId, bool correction = false)
    {
        var column = correction ? "CorrectionDeadlineUtc" : "ScoreConfirmationDeadlineUtc";
        await using var db = app.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            $"UPDATE tournament.matches SET \"{column}\" = now() - interval '10 minutes' WHERE \"Id\" = {{0}}",
            matchId);
    }

    // ---------- browser helpers ----------

    internal static ILocator MatchDialog(IPage page) =>
        page.Locator("[role='dialog'][aria-labelledby='match-dialog-title']");

    /// <summary>
    /// Builds an application URL from a relative path. The fixture's BaseUrl always ends with a
    /// trailing slash, so interpolating a leading slash would produce a doubled '//' path.
    /// </summary>
    internal static string Route(PlaywrightE2EFixture app, string relativePath) =>
        new Uri(new Uri(app.BaseUrl), relativePath).ToString();

    /// <summary>Closes browser contexts through the fixture so failure traces are always written.</summary>
    internal static async Task CloseContextsAsync(PlaywrightE2EFixture app, params IBrowserContext[] contexts)
    {
        foreach (var context in contexts)
            await app.CloseContextAsync(context);
    }

    internal static ILocator Section(ILocator dialog, string ariaLabel) =>
        dialog.Locator($"[aria-label='{ariaLabel}']");

    internal static ILocator MatchCards(IPage page, string participantUsername) =>
        page.Locator(".bracket-match").Filter(new() { HasText = participantUsername });

    /// <summary>Blazor-safe click for a locator that already belongs to a page.</summary>
    internal static Task ClickAsync(ILocator target) => target.Page.ClickWhenInteractiveAsync(target);

    /// <summary>Opens the details dialog from the bracket card that contains the participant.</summary>
    internal static async Task<ILocator> OpenMatchAsync(IPage page, string participantUsername)
    {
        await MatchE2E.ClickAsync(MatchCards(page, participantUsername).First);
        var dialog = MatchDialog(page);
        await Expect(dialog).ToBeVisibleAsync();
        return dialog;
    }

    /// <summary>Opens the details dialog from the upcoming-matches list entry point.</summary>
    internal static async Task<ILocator> OpenMatchFromScheduleAsync(IPage page, string participantUsername)
    {
        var entry = page
            .GetByRole(AriaRole.Button, new()
            {
                NameRegex = new Regex($"^View details for .*{Regex.Escape(participantUsername)}.*$")
            })
            .First;
        await MatchE2E.ClickAsync(entry);
        var dialog = MatchDialog(page);
        await Expect(dialog).ToBeVisibleAsync();
        return dialog;
    }

    internal static async Task CloseDialogAsync(ILocator dialog)
    {
        await MatchE2E.ClickAsync(dialog.GetByRole(AriaRole.Button, new() { Name = "Close match details" }));
        await Expect(dialog).ToBeHiddenAsync();
    }

    internal static async Task ConfirmEndedAsync(ILocator dialog)
    {
        var button = dialog.GetByRole(AriaRole.Button, new() { Name = ConfirmEndedButton });
        await Expect(button).ToBeVisibleAsync();
        await MatchE2E.ClickAsync(button);
        await Expect(button).ToBeHiddenAsync();
    }

    internal static async Task SubmitScoreAsync(
        ILocator dialog,
        int participant1Score,
        int participant2Score)
    {
        // Scores use the match's participant slots, which may not match registration order.
        await dialog.Locator("#participant1-score").FillAsync(participant1Score.ToString());
        var lastScore = dialog.Locator("#participant2-score");
        await lastScore.FillAsync(participant2Score.ToString());
        // The score fields bind on change, so the field that was edited last must lose focus
        // before the submit button (gated on the bound values) can become clickable. Tabbing
        // away is exactly what a user does after typing the final score.
        await lastScore.PressAsync("Tab");
        var submit = dialog.GetByRole(AriaRole.Button, new() { Name = SubmitScoreButton });
        await MatchE2E.ClickAsync(submit);
        // Different valid reports immediately enter the correction state, where the editor stays open.
        await Expect(dialog.Page.Locator(".blazored-toast-message").Last)
            .ToHaveTextAsync(new Regex($"^(?:{Regex.Escape(ScoreReportSaved)}|{Regex.Escape(ActionSaveFailed)})$"));
    }

    /// <summary>Fills one of the administrator resolve score fields, by participant name.</summary>
    internal static async Task FillAdminScoreAsync(ILocator adminPanel, string participantName, int score)
    {
        var field = adminPanel.GetByRole(AriaRole.Spinbutton, new() { Name = participantName });
        await field.FillAsync(score.ToString());
        // Same change-committed binding as the participant editor: leave the field so the
        // server-side gate above the Resolve button can clear.
        await field.PressAsync("Tab");
    }

    internal sealed record MatchView(
        Guid Id,
        int RoundNumber,
        int MatchNumber,
        bool IsLowerBracketMatch,
        Guid? UserParticipant1Id,
        Guid? UserParticipant2Id)
    {
        internal static MatchView From(JsonElement element) => new(
            element.GetProperty("id").GetGuid(),
            element.GetProperty("roundNumber").GetInt32(),
            element.GetProperty("matchNumber").GetInt32(),
            element.GetProperty("isLowerBracketMatch").GetBoolean(),
            ReadGuid(element, "userParticipant1Id"),
            ReadGuid(element, "userParticipant2Id"));

        private static Guid? ReadGuid(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetGuid()
                : null;
    }
}
