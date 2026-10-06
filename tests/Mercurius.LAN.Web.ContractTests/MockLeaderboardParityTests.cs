using System.Text.Json;
using System.Text.Json.Serialization;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.Mock;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MockLeaderboardParityTests
{
    private static readonly Guid ScoreFixtureTournamentId = Guid.Parse("1a111111-1111-1111-1111-111111111111");
    private static readonly Guid TimeFixtureTournamentId = Guid.Parse("1a111111-1111-1111-1111-111111111112");
    private static readonly Guid TrackmaniaFirstUserId = Guid.Parse("41111111-1111-1111-1111-111111111118");
    private static readonly Guid TrackmaniaSecondUserId = Guid.Parse("41111111-1111-1111-1111-111111111119");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void PublicLeaderboardUsesCompetitionRanksForTiedBestResults()
    {
        var store = CreateStore();

        var leaderboard = store.GetLeaderboard(TimeFixtureTournamentId);

        Assert.Equal(LeaderboardRankingMetric.FastestTime, leaderboard.RankingMetric);
        Assert.Equal([1, 1, 3], leaderboard.Rows.Select(row => row.Rank).ToArray());
        Assert.Equal(39_480, leaderboard.Rows[0].DurationMilliseconds);
        Assert.Equal(39_480, leaderboard.Rows[1].DurationMilliseconds);
        Assert.Equal(47_120, leaderboard.Rows[2].DurationMilliseconds);
        Assert.All(leaderboard.Rows, row => Assert.Null(row.Score));
        Assert.Contains(leaderboard.Rows, row => row.ParticipantKind == LeaderboardParticipantKind.Guest);
    }

    [Fact]
    public void TournamentProjectionDoesNotExposeLeaderboardAttemptHistory()
    {
        var store = CreateStore();

        var payload = JsonSerializer.Serialize(store.GetTournament(TimeFixtureTournamentId), JsonOptions);

        Assert.DoesNotContain("rowVersion", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attempts", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorseAttemptKeepsBestResultAndKeepsAttemptHistory()
    {
        var store = CreateStore();
        var participant = store
            .GetAdminLeaderboard(TimeFixtureTournamentId)
            .Participants
            .Single(candidate =>
                candidate.DisplayName == "Speedy Sam" &&
                candidate.Attempts.Any(attempt => attempt.DurationMilliseconds == 39_480));
        var beforeRows = store.GetLeaderboard(TimeFixtureTournamentId).Rows;

        var updated = store.RecordLeaderboardAttempt(
            TimeFixtureTournamentId,
            new RecordLeaderboardAttemptDTO
            {
                ParticipantId = participant.Id,
                DurationMilliseconds = 55_000
            });

        Assert.Equal(3, updated.Attempts.Count);
        var afterRows = store.GetLeaderboard(TimeFixtureTournamentId).Rows;
        Assert.Equal(beforeRows.Select(row => row.Rank), afterRows.Select(row => row.Rank));
        Assert.Equal(
            beforeRows.Select(row => row.DurationMilliseconds),
            afterRows.Select(row => row.DurationMilliseconds));
    }

    [Fact]
    public void FirstAttemptForANewLinkedUserCreatesThatParticipant()
    {
        var store = CreateStore();
        var before = store.GetAdminLeaderboard(TimeFixtureTournamentId).Participants.Count;

        var participant = store.RecordLeaderboardAttempt(
            TimeFixtureTournamentId,
            new RecordLeaderboardAttemptDTO
            {
                LinkedUserId = TrackmaniaSecondUserId,
                DurationMilliseconds = 30_000
            });

        Assert.Equal(LeaderboardParticipantKind.LinkedUser, participant.ParticipantKind);
        Assert.Equal(TrackmaniaSecondUserId, participant.LinkedUserId);
        Assert.Equal(before + 1, store.GetAdminLeaderboard(TimeFixtureTournamentId).Participants.Count);

        var ranked = store.GetLeaderboard(TimeFixtureTournamentId).Rows
            .Single(row => row.ParticipantId == participant.Id);
        Assert.Equal(1, ranked.Rank);
        Assert.Equal(30_000, ranked.DurationMilliseconds);
    }

    [Fact]
    public void ReusingALinkedUserAttachesFurtherAttemptsToTheSameParticipant()
    {
        var store = CreateStore();
        var before = store.GetAdminLeaderboard(TimeFixtureTournamentId).Participants.Count;

        var participant = store.RecordLeaderboardAttempt(
            TimeFixtureTournamentId,
            new RecordLeaderboardAttemptDTO
            {
                LinkedUserId = TrackmaniaFirstUserId,
                DurationMilliseconds = 35_000
            });

        Assert.Equal(before, store.GetAdminLeaderboard(TimeFixtureTournamentId).Participants.Count);
        Assert.Equal(3, participant.Attempts.Count);
        Assert.Equal(1, store.GetLeaderboard(TimeFixtureTournamentId).Rows.Single(row => row.ParticipantId == participant.Id).Rank);
    }

    [Fact]
    public void EqualGuestDisplayNamesStayDistinctParticipants()
    {
        var store = CreateStore();

        var first = store.RecordLeaderboardAttempt(
            TimeFixtureTournamentId,
            new RecordLeaderboardAttemptDTO
            {
                GuestDisplayName = "Mirror Match",
                DurationMilliseconds = 40_000
            });
        var second = store.RecordLeaderboardAttempt(
            TimeFixtureTournamentId,
            new RecordLeaderboardAttemptDTO
            {
                GuestDisplayName = "Mirror Match",
                DurationMilliseconds = 41_000
            });

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(
            2,
            store.GetAdminLeaderboard(TimeFixtureTournamentId).Participants.Count(candidate => candidate.DisplayName == "Mirror Match"));
    }

    [Fact]
    public void MetricSpecificValidationRejectsInvalidAttemptValues()
    {
        var store = CreateStore();
        store.SetTournamentLifecycleState(ScoreFixtureTournamentId, TournamentStatus.InProgress);

        Assert.Throws<InvalidOperationException>(() => store.RecordLeaderboardAttempt(
            TimeFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Wrong Metric", Score = 10m }));
        Assert.Throws<InvalidOperationException>(() => store.RecordLeaderboardAttempt(
            TimeFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Zero Time", DurationMilliseconds = 0 }));
        Assert.Throws<InvalidOperationException>(() => store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Negative Score", Score = -1m }));
        Assert.Throws<InvalidOperationException>(() => store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Over Precise", Score = 1.0000001m }));
        Assert.Throws<InvalidOperationException>(() => store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Over Large", Score = 1_000_000_000_000m }));
        Assert.Throws<InvalidOperationException>(() => store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Wrong Metric", DurationMilliseconds = 1_000 }));

        var accepted = store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Valid Score", Score = 1_234.500001m });

        Assert.Equal(1_234.500001m, accepted.Attempts.Single().Score);
    }

    [Fact]
    public void LeaderboardLifecycleStartsEmptyCompletesWithTiedPlacementsAndResets()
    {
        var store = CreateStore();

        store.SetTournamentLifecycleState(ScoreFixtureTournamentId, TournamentStatus.InProgress);
        var started = store.GetTournament(ScoreFixtureTournamentId)!;
        Assert.Equal(TournamentStatus.InProgress, started.Status);
        Assert.Empty(started.Matches);
        Assert.Empty(store.GetLeaderboard(ScoreFixtureTournamentId).Rows);
        Assert.Throws<InvalidOperationException>(() =>
            store.SetTournamentLifecycleState(ScoreFixtureTournamentId, TournamentStatus.Completed));
        Assert.Equal(
            TournamentStatus.InProgress,
            store.GetTournament(ScoreFixtureTournamentId)!.Status);

        store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Guest Winner", Score = 4_100m });
        store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Guest Winner", Score = 4_200m });
        store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { LinkedUserId = TrackmaniaFirstUserId, Score = 4_200m });

        store.SetTournamentLifecycleState(ScoreFixtureTournamentId, TournamentStatus.Completed);
        var completed = store.GetTournament(ScoreFixtureTournamentId)!;
        Assert.Equal(TournamentStatus.Completed, completed.Status);

        var firstPlace = completed.Placements.Single(placement => placement.Place == 1);
        Assert.Equal(2, firstPlace.LeaderboardParticipants.Count);
        Assert.Contains(firstPlace.LeaderboardParticipants, row => row.ParticipantKind == LeaderboardParticipantKind.Guest);
        Assert.All(firstPlace.LeaderboardParticipants, row => Assert.Equal(4_200m, row.Score));
        Assert.Equal(
            completed.Placements.OrderBy(placement => placement.Place).Select(placement => placement.Place),
            store.GetLeaderboard(ScoreFixtureTournamentId).Rows
                .DistinctBy(row => row.Rank)
                .Select(row => row.Rank));

        var attempt = store.GetAdminLeaderboard(ScoreFixtureTournamentId).Participants.First().Attempts.First();
        Assert.Throws<InvalidOperationException>(() =>
            store.DeleteLeaderboardAttempt(ScoreFixtureTournamentId, attempt.Id, attempt.RowVersion));

        store.SetTournamentLifecycleState(ScoreFixtureTournamentId, TournamentStatus.Scheduled);
        var reset = store.GetTournament(ScoreFixtureTournamentId)!;
        Assert.Equal(TournamentStatus.Scheduled, reset.Status);
        Assert.Empty(reset.Placements);
        Assert.Empty(reset.Matches);
        Assert.Empty(store.GetLeaderboard(ScoreFixtureTournamentId).Rows);
        Assert.Empty(store.GetAdminLeaderboard(ScoreFixtureTournamentId).Participants);
    }

    [Fact]
    public void CorrectionAndRemovalRecalculateTheRankingAndRejectStaleRowVersions()
    {
        var store = CreateStore();
        var participant = store
            .GetAdminLeaderboard(TimeFixtureTournamentId)
            .Participants
            .Single(candidate =>
                candidate.DisplayName == "Speedy Sam" &&
                candidate.Attempts.Any(attempt => attempt.DurationMilliseconds == 47_120));
        var attempt = participant.Attempts.Single();
        var originalRowVersion = attempt.RowVersion;

        var corrected = store.UpdateLeaderboardAttempt(
            TimeFixtureTournamentId,
            attempt.Id,
            new UpdateLeaderboardAttemptDTO
            {
                DurationMilliseconds = 20_000,
                RowVersion = originalRowVersion
            });

        Assert.Equal(20_000, corrected.DurationMilliseconds);
        Assert.Equal(
            1,
            store.GetLeaderboard(TimeFixtureTournamentId).Rows.Single(row => row.ParticipantId == participant.Id).Rank);

        Assert.Throws<LeaderboardConflictException>(() => store.UpdateLeaderboardAttempt(
            TimeFixtureTournamentId,
            attempt.Id,
            new UpdateLeaderboardAttemptDTO
            {
                DurationMilliseconds = 1_000,
                RowVersion = originalRowVersion
            }));

        store.DeleteLeaderboardAttempt(TimeFixtureTournamentId, attempt.Id, corrected.RowVersion);

        Assert.DoesNotContain(
            store.GetLeaderboard(TimeFixtureTournamentId).Rows,
            row => row.ParticipantId == participant.Id);
    }

    [Fact]
    public void ResultMutationsAreRejectedOutsideALeaderboardInProgressState()
    {
        var store = CreateStore();

        Assert.Throws<InvalidOperationException>(() => store.RecordLeaderboardAttempt(
            ScoreFixtureTournamentId,
            new RecordLeaderboardAttemptDTO { GuestDisplayName = "Not Started", Score = 1m }));
        Assert.Throws<InvalidOperationException>(() => store.GetLeaderboard(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        Assert.Throws<InvalidOperationException>(() => store.GetAdminLeaderboard(Guid.Parse("11111111-1111-1111-1111-111111111111")));
    }

    [Fact]
    public void CreatingALeaderboardTournamentValidatesAndNormalizesItsConfiguration()
    {
        var store = CreateStore();

        var created = store.CreateTournament(new CreateTournamentDTO
        {
            Name = "Configuration Parity",
            BracketType = BracketType.Leaderboard,
            LeaderboardRankingMetric = LeaderboardRankingMetric.FastestTime,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(1),
            AverageGameDurationMinutes = 45,
            RoundBreakDurationMinutes = 15
        });

        Assert.Equal(LeaderboardRankingMetric.FastestTime, created.LeaderboardRankingMetric);
        Assert.Equal(ParticipationMode.Individual, created.ParticipationMode);
        Assert.Equal(0, created.AverageGameDurationMinutes);
        Assert.Equal(0, created.RoundBreakDurationMinutes);

        Assert.Throws<InvalidOperationException>(() => store.CreateTournament(new CreateTournamentDTO
        {
            Name = "Missing Metric",
            BracketType = BracketType.Leaderboard,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(1),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10
        }));

        Assert.Throws<InvalidOperationException>(() => store.CreateTournament(new CreateTournamentDTO
        {
            Name = "Team Leaderboard",
            BracketType = BracketType.Leaderboard,
            LeaderboardRankingMetric = LeaderboardRankingMetric.HighestScore,
            ParticipationMode = ParticipationMode.Team,
            PlannedStartTime = DateTime.UtcNow.AddDays(1),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10
        }));

        Assert.Throws<InvalidOperationException>(() => store.CreateTournament(new CreateTournamentDTO
        {
            Name = "Metric On A Bracket",
            BracketType = BracketType.SingleElimination,
            LeaderboardRankingMetric = LeaderboardRankingMetric.HighestScore,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(1),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10
        }));
    }

    [Fact]
    public void UpdatingALeaderboardTournamentValidatesAndNormalizesItsConfiguration()
    {
        var store = CreateStore();

        var updated = store.UpdateTournament(ScoreFixtureTournamentId, new UpdateTournamentDTO
        {
            Name = "Beat Saber Highscore",
            BracketType = BracketType.Leaderboard,
            LeaderboardRankingMetric = LeaderboardRankingMetric.FastestTime,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(2),
            AverageGameDurationMinutes = 45,
            RoundBreakDurationMinutes = 15
        });

        Assert.Equal(LeaderboardRankingMetric.FastestTime, updated.LeaderboardRankingMetric);
        Assert.Equal(0, updated.AverageGameDurationMinutes);
        Assert.Equal(0, updated.RoundBreakDurationMinutes);

        Assert.Throws<InvalidOperationException>(() => store.UpdateTournament(ScoreFixtureTournamentId, new UpdateTournamentDTO
        {
            Name = "Beat Saber Highscore",
            BracketType = BracketType.Leaderboard,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(2),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10
        }));

        Assert.Throws<InvalidOperationException>(() => store.UpdateTournament(ScoreFixtureTournamentId, new UpdateTournamentDTO
        {
            Name = "Beat Saber Highscore",
            BracketType = BracketType.SingleElimination,
            LeaderboardRankingMetric = LeaderboardRankingMetric.FastestTime,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(2),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10
        }));

        var cleared = store.UpdateTournament(ScoreFixtureTournamentId, new UpdateTournamentDTO
        {
            Name = "Beat Saber Highscore",
            BracketType = BracketType.SingleElimination,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(2),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10
        });

        Assert.Null(cleared.LeaderboardRankingMetric);
        Assert.Equal(30, cleared.AverageGameDurationMinutes);
        Assert.Equal(10, cleared.RoundBreakDurationMinutes);
    }

    [Fact]
    public void RejectedStartedTournamentConfigurationUpdateLeavesTheStoredProjectionUnchanged()
    {
        var store = CreateStore();
        var before = store.GetTournament(TimeFixtureTournamentId)!;

        Assert.Throws<InvalidOperationException>(() => store.UpdateTournament(TimeFixtureTournamentId, new UpdateTournamentDTO
        {
            Name = "Rejected replacement",
            Format = TournamentFormat.BestOf3,
            FinalsFormat = TournamentFormat.BestOf5,
            BracketType = BracketType.DoubleElimination,
            ParticipationMode = ParticipationMode.Team,
            TeamSize = 4,
            PlannedStartTime = before.PlannedStartTime.AddDays(3),
            AverageGameDurationMinutes = 75,
            RoundBreakDurationMinutes = 20
        }));

        var after = store.GetTournament(TimeFixtureTournamentId)!;
        Assert.Equal(
            JsonSerializer.Serialize(before, JsonOptions),
            JsonSerializer.Serialize(after, JsonOptions));
    }

    private static MockBackendStore CreateStore()
    {
        var repositoryRoot = FindRepositoryRoot();
        return new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory != null)
        {
            if(File.Exists(Path.Combine(directory.FullName, "src", "Mercurius.LAN.Web", "Mercurius.LAN.Web.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the LAN party repository root.");
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Mercurius.LAN.Web.ContractTests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
