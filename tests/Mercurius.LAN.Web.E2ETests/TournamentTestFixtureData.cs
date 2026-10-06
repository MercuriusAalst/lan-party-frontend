namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Base for the tournament / registration / leaderboard / sponsor suites.
///
/// <see cref="E2ETestBase"/> truncates the isolated fixture database before every test, so a test
/// only ever sees the data it sets up itself and never needs to delete it afterwards. This base just
/// exposes short helpers for building a tournament precondition through the real admin API.
/// </summary>
public abstract class TournamentE2ETestBase : E2ETestBase
{
    protected TournamentE2ETestBase(PlaywrightE2EFixture app) : base(app) { }

    /// <summary>Creates a scheduled tournament through the admin API, exactly like the UI's admin form.</summary>
    protected static Task<Guid> CreateTournamentAsync(
        HttpClient admin,
        string name,
        string bracketType,
        string participationMode,
        string format = "BestOf1",
        string finalsFormat = "BestOf1",
        int? teamSize = null,
        string? rankingMetric = null) =>
        TournamentE2E.CreateTournamentAsync(
            admin, name, bracketType, participationMode, format, finalsFormat, teamSize, rankingMetric);

    protected static Task<Guid> CreateIndividualTournamentAsync(HttpClient admin, string name) =>
        TournamentE2E.CreateScheduledIndividualTournamentAsync(admin, name);

    protected static Task<Guid> CreateTeamTournamentAsync(HttpClient admin, string name, int teamSize) =>
        TournamentE2E.CreateScheduledTeamTournamentAsync(admin, name, teamSize);

    protected static Task<Guid> CreateLeaderboardTournamentAsync(HttpClient admin, string name, string metric) =>
        TournamentE2E.CreateLeaderboardTournamentAsync(admin, name, metric);

    protected static Task<int> CreateSponsorAsync(
        HttpClient admin,
        string name,
        string tier,
        string infoUrl,
        string? description = null) =>
        TournamentE2E.CreateSponsorAsync(admin, name, tier, infoUrl, description);
}
