using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Mock;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MockBackendParityTests
{
    private static readonly Guid FeaturedTournamentId = Guid.Parse("11111111-1111-1111-1111-111111111112");

    [Fact]
    public void FeaturedFixture_ProjectsEverySeededTeamRegistration()
    {
        var store = CreateStore();

        var tournament = store.GetTournament(FeaturedTournamentId);

        Assert.NotNull(tournament);
        Assert.Equal(16, tournament!.Teams.Count());
        Assert.Equal(tournament.Teams.Count(), tournament.Registrations.Count(registration =>
            registration.Kind == TournamentRegistrationKind.Team &&
            registration.Status == TournamentRegistrationStatus.Active));
    }

    [Fact]
    public void Lifecycle_StartCompleteAndResetMatchesLiveStateShape()
    {
        var store = CreateStore();

        store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Canceled);
        store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Scheduled);
        store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.InProgress);

        var started = store.GetTournament(FeaturedTournamentId)!;
        Assert.Equal(TournamentStatus.InProgress, started.Status);
        Assert.NotEmpty(started.Matches);
        Assert.All(started.Matches, match => Assert.Equal(FeaturedTournamentId, match.TournamentId));
        Assert.Contains(started.Matches, match => !match.IsLowerBracketMatch);
        Assert.Contains(started.Matches, match => match.IsLowerBracketMatch);
        Assert.True(started.Matches.Max(match => match.RoundNumber) > 1);
        Assert.Contains(started.Matches, match => match.WinnerNextMatchId.HasValue);
        Assert.All(started.Matches, match =>
        {
            Assert.True(match.EstimatedStartTime.HasValue);
            Assert.True(match.EstimatedEndTime > match.EstimatedStartTime);
        });
        Assert.NotNull(started.EstimatedEndTime);

        var completionStore = CreateStore();
        var existingFinalMatch = completionStore.GetTournament(FeaturedTournamentId)!.Matches
            .OrderByDescending(match => match.RoundNumber)
            .ThenByDescending(match => match.MatchNumber)
            .First();
        completionStore.UpdateMatch(existingFinalMatch.Id, new UpdateMatchDTO { Participant1Score = 1, Participant2Score = 0 });
        completionStore.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Completed);
        var completed = completionStore.GetTournament(FeaturedTournamentId)!;
        Assert.Equal(TournamentStatus.Completed, completed.Status);
        Assert.NotEmpty(completed.Placements);

        completionStore.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Scheduled);
        var reset = completionStore.GetTournament(FeaturedTournamentId)!;
        Assert.Equal(TournamentStatus.Scheduled, reset.Status);
        Assert.Empty(reset.Matches);
        Assert.Empty(reset.Placements);
        Assert.Null(reset.EstimatedEndTime);
        Assert.Equal(DateTime.MinValue, reset.StartTime);
        Assert.Equal(DateTime.MinValue, reset.EndTime);
    }

    [Fact]
    public void Lifecycle_RejectsInvalidTransitionsAndInsufficientParticipants()
    {
        var store = CreateStore();
        Assert.Throws<InvalidOperationException>(() =>
            store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Completed));

        var inProgress = store.GetTournament(FeaturedTournamentId)!;
        Assert.Throws<InvalidOperationException>(() =>
            store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Scheduled));
        Assert.Equal(TournamentStatus.InProgress, store.GetTournament(FeaturedTournamentId)!.Status);
        Assert.Equal(inProgress.Matches.Select(match => match.Id),
            store.GetTournament(FeaturedTournamentId)!.Matches.Select(match => match.Id));
        Assert.Throws<InvalidOperationException>(() => store.DeleteTournament(FeaturedTournamentId));
        Assert.NotNull(store.GetTournament(FeaturedTournamentId));

        store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Canceled);
        store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Scheduled);
        Assert.Throws<InvalidOperationException>(() =>
            store.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Completed));

        var emptyTournament = store.CreateTournament(new CreateTournamentDTO
        {
            Name = "Needs Participants",
            BracketType = BracketType.SingleElimination,
            Format = TournamentFormat.BestOf1,
            FinalsFormat = TournamentFormat.BestOf1,
            ParticipationMode = ParticipationMode.Individual,
            PlannedStartTime = DateTime.UtcNow.AddDays(1),
            AverageGameDurationMinutes = 30,
            RoundBreakDurationMinutes = 10
        });

        Assert.Throws<InvalidOperationException>(() =>
            store.SetTournamentLifecycleState(emptyTournament.Id, TournamentStatus.InProgress));

        var completionStore = CreateStore();
        var finalMatch = completionStore.GetTournament(FeaturedTournamentId)!.Matches
            .OrderByDescending(match => match.RoundNumber)
            .ThenByDescending(match => match.MatchNumber)
            .First();
        completionStore.UpdateMatch(finalMatch.Id, new UpdateMatchDTO { Participant1Score = 1, Participant2Score = 0 });
        completionStore.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Completed);
        Assert.Throws<InvalidOperationException>(() =>
            completionStore.SetTournamentLifecycleState(FeaturedTournamentId, TournamentStatus.Canceled));
    }

    [Fact]
    public void LocalFixtureHasUniqueEntitiesAndResolvableProfileNavigation()
    {
        var store = CreateStore();
        var users = store.GetUsers().Where(user => !user.IsDeleted).ToList();
        Assert.Equal(users.Count, users.Select(user => user.Id).Distinct().Count());
        Assert.Equal(users.Count, users.Select(user => user.Username).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var teams = store.GetTeams(1, 1_000);
        Assert.Equal(teams.Count, teams.Select(team => team.Id).Distinct().Count());
        Assert.Equal(teams.Count, teams.Select(team => team.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach(var team in teams)
        {
            Assert.NotNull(store.GetPublicTeamByName(team.Name));
            Assert.Single(team.Members, member => member.Id == team.CaptainUserId);
            Assert.Equal(team.Members.Count(), team.Members.Select(member => member.Id).Distinct().Count());
            foreach(var member in team.Members)
            {
                var user = Assert.Single(users, candidate => candidate.Id == member.Id);
                Assert.Equal(user.Username, member.Username);
                Assert.Equal(member.Username, store.GetPublicUserByUsername(member.Username!)!.Username);
            }
        }
        var matches = store.GetTournaments().SelectMany(tournament => store.GetTournament(tournament.Id)!.Matches).ToList();
        Assert.Equal(matches.Count, matches.Select(match => match.Id).Distinct().Count());
        foreach(var match in matches)
            Assert.Equal(match.TournamentId, store.GetMatchActionState("admin", match.Id).Match.TournamentId);
    }

    public static IEnumerable<object[]> LocalTournamentIds() =>
        CreateStore().GetTournaments().Select(tournament => new object[] { tournament.Id.ToString() });

    [Theory]
    [MemberData(nameof(LocalTournamentIds))]
    public void LocalTournamentFixtureHasValidRegistrationsStateAndMatchGraph(string id)
    {
        var store = CreateStore();
        var tournament = store.GetTournament(Guid.Parse(id))!;
        var teams = store.GetTeams(1, 1_000).ToDictionary(team => team.Id);
        var users = store.GetUsers().Where(user => !user.IsDeleted).ToDictionary(user => user.Id);
        var teamMode = tournament.ParticipationMode == ParticipationMode.Team;
        var participantIds = tournament.Registrations.Select(registration => teamMode ? registration.Team!.Id : registration.User!.Id).ToHashSet();
        Assert.Equal(tournament.Registrations.Count(), participantIds.Count);
        Assert.Equal(participantIds.OrderBy(value => value), (teamMode
            ? tournament.Teams.Select(team => team.Id)
            : tournament.Users.Select(user => user.Id)).OrderBy(value => value));
        foreach(var registration in tournament.Registrations)
        {
            Assert.Equal(tournament.Id, registration.TournamentId);
            Assert.Equal(TournamentRegistrationStatus.Active, registration.Status);
            Assert.Equal(teamMode ? TournamentRegistrationKind.Team : TournamentRegistrationKind.Individual, registration.Kind);
            if(teamMode)
            {
                var team = teams[registration.Team!.Id];
                Assert.Equal(tournament.TeamSize, registration.RosterMembers.Count);
                Assert.Equal(registration.RosterMembers.Count, registration.RosterMembers.Select(member => member.User.Id).Distinct().Count());
                Assert.Single(registration.RosterMembers, member => member.IsCaptain && member.User.Id == team.CaptainUserId);
                Assert.All(registration.RosterMembers, member => Assert.Contains(team.Members, candidate => candidate.Id == member.User.Id));
                Assert.All(registration.RosterMembers, member => Assert.Equal(users[member.User.Id].Username, member.User.Username));
            }
            else
                Assert.Equal(users[registration.User!.Id].Username, registration.User.Username);
        }
        var registeredUsers = tournament.Registrations.SelectMany(registration => teamMode
            ? registration.RosterMembers.Select(member => member.User.Id)
            : new[] { registration.User!.Id }).ToList();
        Assert.Equal(registeredUsers.Count, registeredUsers.Distinct().Count());
        if(tournament.Status == TournamentStatus.Scheduled)
        {
            Assert.Equal(default, tournament.StartTime);
            Assert.Empty(tournament.Matches);
            Assert.Empty(tournament.Placements);
        }
        if(tournament.Status != TournamentStatus.Completed)
            Assert.Equal(default, tournament.EndTime);
        var matches = tournament.Matches.ToDictionary(match => match.Id);
        foreach(var match in matches.Values)
        {
            Assert.Equal(tournament.Id, match.TournamentId);
            Assert.Equal(tournament.ParticipationMode, match.ParticipationMode);
            Assert.Equal(tournament.BracketType, match.BracketType);
            var first = teamMode ? match.TeamParticipant1Id : match.UserParticipant1Id;
            var second = teamMode ? match.TeamParticipant2Id : match.UserParticipant2Id;
            var winner = teamMode ? match.TeamWinnerId : match.UserWinnerId;
            var loser = teamMode ? match.TeamLoserId : match.UserLoserId;
            Assert.All(new[] { first, second, winner, loser }.Where(value => value.HasValue), value => Assert.Contains(value!.Value, participantIds));
            if(first.HasValue && second.HasValue)
                Assert.NotEqual(first, second);
            Assert.Null(teamMode ? match.UserParticipant1Id : match.TeamParticipant1Id);
            Assert.Null(teamMode ? match.UserParticipant2Id : match.TeamParticipant2Id);
            if(winner.HasValue)
            {
                Assert.Contains(winner, new[] { first, second });
                Assert.Equal(winner == first ? second : first, loser);
                Assert.Equal(Mercurius.LAN.Web.DTOs.Matches.MatchLifecycleState.Completed, match.LifecycleState);
                Assert.True(match.EndTime >= match.StartTime);
                Assert.True(match.Participant1Ended && match.Participant2Ended);
                Assert.True(match.ResultVersion > 0);
                Assert.NotNull(match.ResultRecordedAtUtc);
                Assert.True(winner == first ? match.Participant1Score > match.Participant2Score : match.Participant2Score > match.Participant1Score);
                var winningScore = winner == first ? match.Participant1Score : match.Participant2Score;
                Assert.Equal(match.Format switch { TournamentFormat.BestOf1 => 1, TournamentFormat.BestOf3 => 2, _ => 3 }, winningScore);
            }
            else
            {
                Assert.Equal(default, match.EndTime);
                Assert.Null(match.Participant1Score);
                Assert.Null(match.Participant2Score);
            }
            foreach(var (nextId, participant) in new[] { (match.WinnerNextMatchId, winner), (match.LoserNextMatchId, loser) })
            {
                if(!nextId.HasValue)
                    continue;
                var next = matches[nextId.Value];
                Assert.NotEqual(match.Id, next.Id);
                Assert.True(next.RoundNumber > match.RoundNumber ||
                    (next.RoundNumber == match.RoundNumber && next.IsLowerBracketMatch && !match.IsLowerBracketMatch));
                Assert.True(next.EstimatedStartTime >= match.EstimatedEndTime);
                if(!participant.HasValue)
                    continue;
                var nextFirst = teamMode ? next.TeamParticipant1Id : next.UserParticipant1Id;
                var nextSecond = teamMode ? next.TeamParticipant2Id : next.UserParticipant2Id;
                Assert.Contains(participant, new[] { nextFirst, nextSecond });
                Assert.Equal(match.Id, participant == nextFirst ? next.Participant1SourceMatchId : next.Participant2SourceMatchId);
            }
            foreach(var sourceId in new[] { match.Participant1SourceMatchId, match.Participant2SourceMatchId }.Where(value => value.HasValue))
                Assert.Contains(sourceId!.Value, matches.Keys);
        }
        if(tournament.Status == TournamentStatus.Completed)
        {
            Assert.NotEmpty(tournament.Placements);
            var placedIds = tournament.Placements.SelectMany(placement => teamMode
                ? placement.Teams.Select(team => team.Id)
                : placement.Users.Select(user => user.Id)).ToList();
            Assert.Equal(placedIds.Count, placedIds.Distinct().Count());
            Assert.All(placedIds, value => Assert.Contains(value, participantIds));
            var final = matches.Values.OrderByDescending(match => match.RoundNumber).First();
            var firstPlace = Assert.Single(tournament.Placements, placement => placement.Place == 1);
            Assert.Equal(teamMode ? final.TeamWinnerId : final.UserWinnerId,
                teamMode ? Assert.Single(firstPlace.Teams).Id : Assert.Single(firstPlace.Users).Id);
            Assert.All(matches.Values, match => Assert.NotNull(teamMode ? match.TeamWinnerId : match.UserWinnerId));
        }
    }
    private static MockBackendStore CreateStore()
    {
        var repositoryRoot = FindRepositoryRoot();
        return new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine("src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));
    }

    private static string FindRepositoryRoot()
    {
        foreach(var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while(directory is not null)
            {
                if(File.Exists(Path.Combine(directory.FullName, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for the mock fixture.");
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(MockBackendParityTests);
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
