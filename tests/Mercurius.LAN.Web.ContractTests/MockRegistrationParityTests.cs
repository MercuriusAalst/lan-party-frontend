using System.Text.Json;
using System.Reflection;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Mock;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MockRegistrationParityTests
{
    private static readonly Guid TournamentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TeamId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid DefaultRegistrationTeamId = Guid.Parse("21111111-1111-1111-1111-111111111120");
    private static readonly Guid RegistrationDemoTournamentId = Guid.Parse("11111111-1111-1111-1111-111111111115");
    private static readonly Guid CounterStrike2TournamentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RocketLeagueTournamentId = Guid.Parse("11111111-1111-1111-1111-111111111114");
    private static readonly Guid SharedTeamAlphaId = Guid.Parse("21111111-1111-1111-1111-111111111111");
    private static readonly Guid SharedEchoTeamId = Guid.Parse("21111111-1111-1111-1111-111111111115");
    private static readonly Guid CounterStrike2RegistrationTeamId = Guid.Parse("21111111-1111-1111-1111-111111111141");
    private static readonly Guid FeaturedDoubleEliminationTournamentId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    private static readonly Guid CaptainId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid MemberId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void TeamSizeOneAcceptsCaptainOnlyAndAllowsAnEditWithoutDuplicateFailure()
    {
        using var fixture = CreateFixture(teamSize: 1);

        var eligibility = fixture.Store.CheckTeamTournamentRegistrationEligibility("user", TournamentId, TeamId);
        Assert.True(eligibility.Eligible);

        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId] });

        Assert.Equal(TournamentRegistrationStatus.Active, registration.Status);
        Assert.Single(registration.RosterMembers);
        Assert.Equal(RosterMemberConfirmationStatus.AutoConfirmed, registration.RosterMembers[0].ConfirmationStatus);

        var duplicateEligibility = fixture.Store.CheckTeamRosterEligibility(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId] });

        Assert.False(duplicateEligibility.Eligible);
        Assert.Contains("team_already_registered", duplicateEligibility.ReasonCodes);
        Assert.Contains("duplicate_participation", duplicateEligibility.Candidates.Single().ReasonCodes);

        var editedRegistration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId] });

        Assert.Equal(TournamentRegistrationStatus.Active, editedRegistration.Status);
    }

    [Fact]
    public void TeamMemberCannotDeleteCaptainOwnedRegistration()
    {
        using var fixture = CreateFixture(teamSize: 2);

        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId, MemberId] });

        Assert.Equal(TournamentRegistrationStatus.PendingConfirmation, registration.Status);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            fixture.Store.DeleteTeamTournamentRegistration("admin", TournamentId, TeamId));

        Assert.Equal("Team registration not found.", exception.Message);
    }

    [Fact]
    public void RosterEligibilityReportsDuplicateAndUnknownCandidates()
    {
        using var fixture = CreateFixture(teamSize: 2);

        var duplicate = fixture.Store.CheckTeamRosterEligibility(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId, CaptainId] });

        Assert.False(duplicate.Eligible);
        Assert.Contains("roster_user_ids_must_be_unique", duplicate.ReasonCodes);

        var unknownUserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var unknown = fixture.Store.CheckTeamRosterEligibility(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId, unknownUserId] });

        var unknownCandidate = unknown.Candidates.Single(candidate => candidate.UserId == unknownUserId);
        Assert.Contains("user_not_found", unknownCandidate.ReasonCodes);
        Assert.Contains("not_team_member", unknownCandidate.ReasonCodes);

        var nullRoster = fixture.Store.CheckTeamRosterEligibility("user", TournamentId, TeamId, null!);
        Assert.Contains("exact_roster_size_required", nullRoster.ReasonCodes);
        Assert.Contains("captain_required", nullRoster.ReasonCodes);
    }

    [Fact]
    public void PendingRosterConfirmationIsRevalidatedAndCannotBeRepeated()
    {
        using var fixture = CreateFixture(teamSize: 2);

        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId, MemberId] });
        var pendingMemberId = registration.RosterMembers
            .Single(member => member.User.Id == MemberId)
            .Id;

        var confirmed = fixture.Store.ConfirmTournamentRosterMember("admin", TournamentId, pendingMemberId);

        Assert.Equal(RosterMemberConfirmationStatus.Confirmed,
            confirmed.RosterMembers.Single(member => member.Id == pendingMemberId).ConfirmationStatus);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            fixture.Store.ConfirmTournamentRosterMember("admin", TournamentId, pendingMemberId));
        Assert.Equal("Pending roster confirmation not found.", exception.Message);
    }

    [Fact]
    public void PendingRosterSelectionIsReloadSafeAndDeclineKeepsTeamRegistration()
    {
        using var fixture = CreateFixture(teamSize: 2);
        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId, MemberId] });
        var pendingMemberId = registration.RosterMembers.Single(member => member.User.Id == MemberId).Id;

        var notification = Assert.Single(fixture.Store.GetPendingRosterConfirmations("admin"));
        Assert.Equal(pendingMemberId, notification.RosterMemberId);
        Assert.Equal("Parity Cup", notification.TournamentName);
        Assert.Equal("Parity Team", notification.TeamName);

        fixture.Store.DeclineTournamentRosterMember("admin", TournamentId, pendingMemberId);
        fixture.Store.DeclineTournamentRosterMember("admin", TournamentId, pendingMemberId);

        Assert.Empty(fixture.Store.GetPendingRosterConfirmations("admin"));
        var captainState = fixture.Store.GetCurrentUserTournamentRegistrationState("user", TournamentId);
        var retained = Assert.Single(captainState.CaptainManagedRegistrations);
        Assert.Equal(TournamentRegistrationStatus.PendingConfirmation, retained.Status);
        Assert.Single(retained.RosterMembers);
        Assert.Equal(CaptainId, retained.RosterMembers[0].User.Id);
        Assert.Empty(fixture.Store.GetTournament(TournamentId)!.Registrations);
    }

    [Theory]
    [InlineData(TournamentStatus.Canceled)]
    [InlineData(TournamentStatus.InProgress)]
    public void NonScheduledPendingRosterSelectionIsHiddenAndDeclineIsRejected(TournamentStatus status)
    {
        using var fixture = CreateFixture(teamSize: 2);
        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId, MemberId] });
        var pendingMemberId = registration.RosterMembers.Single(member => member.User.Id == MemberId).Id;

        SetTournamentStatus(fixture.Store, status);

        Assert.Empty(fixture.Store.GetPendingRosterConfirmations("admin"));
        var exception = Assert.Throws<InvalidOperationException>(() =>
            fixture.Store.DeclineTournamentRosterMember("admin", TournamentId, pendingMemberId));

        Assert.Equal("tournament_not_scheduled", exception.Message);
    }

    [Fact]
    public void CurrentTeamRegistrationContextIncludesPendingTeamWithoutLeakingItPublicly()
    {
        using var fixture = CreateFixture(teamSize: 2);

        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            TeamId,
            new SubmitTeamRosterDTO { TeamId = TeamId, UserIds = [CaptainId, MemberId] });

        var memberState = fixture.Store.GetCurrentUserTournamentRegistrationState("admin", TournamentId);
        var currentTeam = memberState.CurrentTeamRegistration;
        Assert.NotNull(currentTeam);

        Assert.Equal(registration.Id, currentTeam.Id);
        Assert.Equal(TournamentRegistrationStatus.PendingConfirmation, currentTeam.Status);
        Assert.Equal(TeamId, currentTeam.Team!.Id);
        Assert.True(memberState.CanConfirmRoster);
        Assert.Null(memberState.ActiveTeamRegistration);
        Assert.Empty(fixture.Store.GetTournament(TournamentId)!.Registrations);
    }

    [Fact]
    public void DefaultMockPersonaHasACompleteCaptainTeamAndPendingMember()
    {
        using var fixture = CreateFixture(teamSize: 2, captainUsername: "mockuser", memberUsername: "mockadmin");

        var teamSummary = fixture.Store.GetCurrentUserTeamSummary("user");
        var summaryTeam = teamSummary.CaptainedTeams.Single(team => team.Id == DefaultRegistrationTeamId);
        Assert.Equal(2, summaryTeam.Members.Count);

        Assert.DoesNotContain(fixture.Store.GetTournament(TournamentId)!.Teams,
            team => team.Id == DefaultRegistrationTeamId);
        var captainTeam = fixture.Store.GetTeams(1, 100)
            .Single(team => team.Id == DefaultRegistrationTeamId);
        Assert.Equal("Mock Registration Crew", captainTeam.Name);
        Assert.Equal(2, captainTeam.Members.Count());

        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            TournamentId,
            DefaultRegistrationTeamId,
            new SubmitTeamRosterDTO
            {
                TeamId = DefaultRegistrationTeamId,
                UserIds = [CaptainId, MemberId]
            });

        Assert.Equal(TournamentRegistrationStatus.PendingConfirmation, registration.Status);
        Assert.True(fixture.Store.GetCurrentUserTournamentRegistrationState("admin", TournamentId).CanConfirmRoster);
    }

    [Fact]
    public void LocalDemoFixtureCompletesTeamRegistrationThroughMemberConfirmation()
    {
        using var fixture = CreateLocalFixture();
        var tournament = fixture.Store.GetTournament(RegistrationDemoTournamentId)!;
        Assert.Equal(TournamentStatus.Scheduled, tournament.Status);
        Assert.Equal(2, tournament.TeamSize);
        Assert.Empty(tournament.Registrations);

        Assert.DoesNotContain(tournament.Teams, candidate => candidate.Id == DefaultRegistrationTeamId);
        var selectedTeam = Assert.Single(TournamentParticipantsTab.FilterCaptainedTeamsByRequiredSize(
            fixture.Store.GetCurrentUserTeamSummary("user").CaptainedTeams,
            tournament.TeamSize!.Value), candidate => candidate.Id == DefaultRegistrationTeamId);
        Assert.Equal(2, selectedTeam.Members.Count);
        var team = Assert.Single(fixture.Store.GetTeams(1, 100), candidate => candidate.Id == DefaultRegistrationTeamId);
        Assert.Equal("mockuser", team.Members.Single(member => member.Id == team.CaptainUserId).Username);
        Assert.Equal("mockadmin", team.Members.Single(member => member.Id != team.CaptainUserId).Username);
        Assert.Empty(fixture.Store.GetCurrentUserTournamentRegistrationState("user", tournament.Id).CaptainManagedRegistrations);
        Assert.True(fixture.Store.CheckTeamTournamentRegistrationEligibility("user", tournament.Id, team.Id).Eligible);
        Assert.True(fixture.Store.CheckTeamRosterEligibility(
            "user",
            tournament.Id,
            team.Id,
            new SubmitTeamRosterDTO
            {
                TeamId = team.Id,
                UserIds = team.Members.Select(member => member.Id).ToList()
            }).Eligible);

        var registration = fixture.Store.SubmitTeamTournamentRoster(
            "user",
            tournament.Id,
            team.Id,
            new SubmitTeamRosterDTO
            {
                TeamId = team.Id,
                UserIds = team.Members.Select(member => member.Id).ToList()
            });
        Assert.Equal(TournamentRegistrationStatus.PendingConfirmation, registration.Status);
        Assert.Empty(fixture.Store.GetTournament(tournament.Id)!.Registrations);

        var pendingMemberId = registration.RosterMembers
            .Single(member => member.User.Username == "mockadmin")
            .Id;
        var confirmed = fixture.Store.ConfirmTournamentRosterMember("admin", tournament.Id, pendingMemberId);

        Assert.Equal(TournamentRegistrationStatus.Active, confirmed.Status);
        Assert.Equal(RosterMemberConfirmationStatus.Confirmed,
            confirmed.RosterMembers.Single(member => member.Id == pendingMemberId).ConfirmationStatus);
        var publicRegistration = Assert.Single(fixture.Store.GetTournament(tournament.Id)!.Registrations);
        Assert.Equal(TournamentRegistrationStatus.Active, publicRegistration.Status);
        Assert.Equal(team.Id, publicRegistration.Team!.Id);
        Assert.Equal(2, publicRegistration.RosterMembers.Count);
        Assert.Equal(
            publicRegistration.RosterMembers.Select(member => member.User.Id).OrderBy(id => id),
            Assert.Single(fixture.Store.GetTournament(tournament.Id)!.Teams).Members.Select(member => member.Id).OrderBy(id => id));
        AssertEffectiveTournamentTeamRostersMatchGlobalCatalog(fixture.Store);
    }

    [Fact]
    public void LocalCounterStrikeFixtureKeepsMockUserEligibleWithoutProjectingUnregisteredTeam()
    {
        using var fixture = CreateLocalFixture();
        var tournament = fixture.Store.GetTournament(CounterStrike2TournamentId)!;
        Assert.Equal(TournamentStatus.Scheduled, tournament.Status);
        Assert.Equal(5, tournament.TeamSize);
        Assert.DoesNotContain(tournament.Registrations,
            registration => registration.Team?.Id == CounterStrike2RegistrationTeamId);
        Assert.DoesNotContain(tournament.Registrations,
            registration => registration.RosterMembers.Any(member => member.User.Username == "mockuser"));
        Assert.DoesNotContain(tournament.Teams, candidate => candidate.Id == CounterStrike2RegistrationTeamId);

        var teamSummary = fixture.Store.GetCurrentUserTeamSummary("user");
        var visibleTeam = Assert.Single(TournamentParticipantsTab.FilterCaptainedTeamsByRequiredSize(
            teamSummary.CaptainedTeams,
            tournament.TeamSize!.Value),
            candidate => candidate.Id == CounterStrike2RegistrationTeamId);
        Assert.NotEqual(DefaultRegistrationTeamId, visibleTeam.Id);
        Assert.Equal(5, visibleTeam.Members.Count);

        var team = Assert.Single(fixture.Store.GetTeams(1, 100), candidate => candidate.Id == CounterStrike2RegistrationTeamId);
        Assert.Equal("mockuser", team.Members.Single(member => member.Id == team.CaptainUserId).Username);
        Assert.Equal(5, team.Members.Count());
        Assert.Equal(5, team.Members.Select(member => member.Id).Distinct().Count());

        Assert.True(fixture.Store.CheckTeamTournamentRegistrationEligibility("user", tournament.Id, team.Id).Eligible);
        Assert.True(fixture.Store.CheckTeamRosterEligibility(
            "user",
            tournament.Id,
            team.Id,
            new SubmitTeamRosterDTO
            {
                TeamId = team.Id,
                UserIds = team.Members.Select(member => member.Id).ToList()
            }).Eligible);
    }

    [Fact]
    public void FeaturedDoubleEliminationSeedUsesFiveDistinctPlayersPerTeam()
    {
        using var fixture = CreateLocalFixture();
        var tournament = fixture.Store.GetTournament(FeaturedDoubleEliminationTournamentId)!;
        var registrations = tournament.Registrations.ToList();

        Assert.Equal(5, tournament.TeamSize);
        Assert.Equal(16, registrations.Count);
        Assert.Equal(16, tournament.Teams.Select(team => team.Id).Distinct().Count());
        Assert.Equal(16, tournament.Teams.Select(team => team.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(registrations, registration =>
        {
            Assert.Equal(5, registration.RosterMembers.Count);
            Assert.Equal(5, registration.RosterMembers.Select(member => member.User.Id).Distinct().Count());
        });
        Assert.Equal(80, registrations
            .SelectMany(registration => registration.RosterMembers)
            .Select(member => member.User.Id)
            .Distinct()
            .Count());

        var document = (MockBackendDocument)typeof(MockBackendStore)
            .GetField("_document", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fixture.Store)!;
        var activeUsers = document.Users.Where(user => !user.IsDeleted).ToList();
        Assert.All(activeUsers, user => Assert.False(string.IsNullOrWhiteSpace(user.Username)));
        Assert.Equal(activeUsers.Count, activeUsers.Select(user => user.Id).Distinct().Count());
        Assert.Equal(activeUsers.Count,
            activeUsers.Select(user => user.Username!).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var gammaTeam = Assert.Single(tournament.Teams, team => team.Name == "Valorant Gamma Grid");
        var generatedTeammate = gammaTeam.Members.Single(member => member.Username == "gamma1-teammate-2");
        Assert.Equal("gamma1-teammate-2", generatedTeammate.Username);
        var generatedUserSummaries = fixture.Store.GetPublicUserMatchSummaries(generatedTeammate.Username!);
        Assert.NotNull(generatedUserSummaries);
        Assert.Contains(generatedUserSummaries!.UpcomingMatches,
            summary => summary.TournamentId == FeaturedDoubleEliminationTournamentId);
        var alphaTeam = Assert.Single(tournament.Teams, team => team.Name == "Valorant Team Alpha");
        Assert.NotNull(fixture.Store.GetPublicUserByUsername(
            alphaTeam.Members.Single(member => member.Username == "alpha1-teammate-2").Username!));
        var echoTeam = Assert.Single(fixture.Store.GetTeams(1, 1_000), team => team.Id == SharedEchoTeamId);
        Assert.Equal(
            new[] { "echo1", "echo2", "frame1", "frame2", "track1" },
            echoTeam.Members.Select(member => member.Username!).OrderBy(username => username, StringComparer.Ordinal));

        var counterStrike = fixture.Store.GetTournament(CounterStrike2TournamentId)!;
        var rocketLeague = fixture.Store.GetTournament(RocketLeagueTournamentId)!;
        var featuredTeamIds = tournament.Teams.Select(team => team.Id).ToHashSet();
        Assert.Empty(counterStrike.Teams.Select(team => team.Id).Intersect(featuredTeamIds));
        Assert.Empty(rocketLeague.Teams.Select(team => team.Id).Intersect(featuredTeamIds));
        var counterStrikeTeam = Assert.Single(counterStrike.Teams, team => team.Id == SharedTeamAlphaId);
        var rocketLeagueTeam = Assert.Single(rocketLeague.Teams, team => team.Id == SharedTeamAlphaId);
        var counterStrikeRegistration = Assert.Single(counterStrike.Registrations, registration => registration.Team?.Id == SharedTeamAlphaId);
        var rocketLeagueRegistration = Assert.Single(rocketLeague.Registrations, registration => registration.Team?.Id == SharedTeamAlphaId);
        var counterStrikeTeamRoster = counterStrikeTeam.Members.Select(member => member.Id).OrderBy(id => id).ToArray();
        var counterStrikeRegistrationRoster = counterStrikeRegistration.RosterMembers.Select(member => member.User.Id).OrderBy(id => id).ToArray();
        var rocketLeagueTeamRoster = rocketLeagueTeam.Members.Select(member => member.Id).OrderBy(id => id).ToArray();
        var rocketLeagueRegistrationRoster = rocketLeagueRegistration.RosterMembers.Select(member => member.User.Id).OrderBy(id => id).ToArray();

        Assert.Equal(counterStrikeRegistrationRoster, counterStrikeTeamRoster);
        Assert.Equal(counterStrikeRegistrationRoster, rocketLeagueRegistrationRoster);
        Assert.Equal(rocketLeagueRegistrationRoster, rocketLeagueTeamRoster);
        AssertEffectiveTournamentTeamRostersMatchGlobalCatalog(fixture.Store);
    }

    private static void AssertEffectiveTournamentTeamRostersMatchGlobalCatalog(MockBackendStore store)
    {
        var globalTeams = store.GetTeams(1, 1_000).ToDictionary(team => team.Id);
        foreach(var tournamentSummary in store.GetTournaments())
        {
            var tournament = store.GetTournament(tournamentSummary.Id)!;
            var registrations = tournament.Registrations
                .Where(registration =>
                    registration.Kind == TournamentRegistrationKind.Team &&
                    registration.Status == TournamentRegistrationStatus.Active)
                .ToList();
            var projectedTeams = tournament.Teams.ToDictionary(team => team.Id);

            Assert.Equal(
                registrations.Select(registration => registration.Team!.Id).OrderBy(id => id),
                projectedTeams.Keys.OrderBy(id => id));
            foreach(var registration in registrations)
            {
                var rosterIds = registration.RosterMembers.Select(member => member.User.Id).OrderBy(id => id);
                var globalTeam = globalTeams[registration.Team!.Id];
                Assert.Equal(rosterIds, globalTeam.Members.Select(member => member.Id).OrderBy(id => id));
                Assert.Equal(rosterIds, projectedTeams[registration.Team.Id].Members.Select(member => member.Id).OrderBy(id => id));
            }
        }
    }

    [Fact]
    public void ClosedTournamentReturnsScheduleReasonAndBlocksMutations()
    {
        using var fixture = CreateFixture(teamSize: 1, status: TournamentStatus.InProgress);

        var eligibility = fixture.Store.CheckTeamTournamentRegistrationEligibility("user", TournamentId, TeamId);

        Assert.False(eligibility.Eligible);
        Assert.Contains("tournament_not_scheduled", eligibility.ReasonCodes);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            fixture.Store.DeleteTeamTournamentRegistration("user", TournamentId, TeamId));

        Assert.Equal("tournament_not_scheduled", exception.Message);
    }

    private static Fixture CreateFixture(
        int teamSize,
        TournamentStatus status = TournamentStatus.Scheduled,
        string captainUsername = "captain",
        string memberUsername = "member")
    {
        var root = Path.Combine(Path.GetTempPath(), "mercurius-lan-registration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "backend.json"), BuildFixtureJson(teamSize, status, captainUsername, memberUsername));

        var environment = new TestHostEnvironment(root);
        var options = Microsoft.Extensions.Options.Options.Create(new MockBackendOptions { DataFilePath = "backend.json" });
        return new Fixture(root, new MockBackendStore(environment, options));
    }

    private static Fixture CreateLocalFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null &&
              !File.Exists(Path.Combine(directory.FullName, "src", "Mercurius.LAN.Web", "Mercurius.LAN.Web.csproj")))
            directory = directory.Parent;
        var repositoryRoot = directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the LAN party repository root.");
        var sourcePath = Path.Combine(
            repositoryRoot,
            "src",
            "Mercurius.LAN.Web",
            "MockData.Local",
            "backend.json");
        var root = Path.Combine(Path.GetTempPath(), "mercurius-lan-registration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.Copy(sourcePath, Path.Combine(root, "backend.json"));

        var environment = new TestHostEnvironment(root);
        var options = Microsoft.Extensions.Options.Options.Create(new MockBackendOptions { DataFilePath = "backend.json" });
        return new Fixture(root, new MockBackendStore(environment, options));
    }

    private static string BuildFixtureJson(
        int teamSize,
        TournamentStatus status,
        string captainUsername,
        string memberUsername)
    {
        var captain = new
        {
            id = CaptainId,
            username = captainUsername,
            firstname = "Casey",
            lastname = "Captain",
            email = "captain@example.test",
            emailVerified = true,
            discordId = "captain#0001",
            steamId = "steam-captain",
            riotId = (string?)null,
            displayName = "Casey Captain",
            isDeleted = false,
            createdAtUtc = "2026-01-01T00:00:00Z",
            updatedAtUtc = "2026-01-01T00:00:00Z"
        };
        var member = new
        {
            id = MemberId,
            username = memberUsername,
            firstname = "Mina",
            lastname = "Member",
            email = "member@example.test",
            emailVerified = true,
            discordId = "member#0001",
            steamId = "steam-member",
            riotId = (string?)null,
            displayName = "Mina Member",
            isDeleted = false,
            createdAtUtc = "2026-01-01T00:00:00Z",
            updatedAtUtc = "2026-01-01T00:00:00Z"
        };

        var document = new
        {
            tournaments = new[]
            {
                new
                {
                    id = TournamentId,
                    name = "Parity Cup",
                    startTime = "2026-06-01T10:00:00Z",
                    endTime = "2026-06-01T16:00:00Z",
                    plannedStartTime = "2026-06-01T10:00:00Z",
                    estimatedEndTime = "2026-06-01T16:00:00Z",
                    status = status.ToString(),
                    bracketType = "SingleElimination",
                    format = "BestOf3",
                    finalsFormat = "BestOf3",
                    participationMode = "Team",
                    teamSize,
                    placements = Array.Empty<object>(),
                    matches = Array.Empty<object>(),
                    users = Array.Empty<object>(),
                    teams = Array.Empty<object>(),
                    registrations = Array.Empty<object>()
                }
            },
            teams = new[]
            {
                new
                {
                    id = TeamId,
                    name = "Parity Team",
                    captainUserId = CaptainId,
                    members = new[] { captain, member },
                    teamInvites = Array.Empty<object>()
                }
            },
            users = new[] { captain, member },
            profiles = new[]
            {
                new { persona = "user", profile = new { isComplete = true, user = captain, email = captain.email, emailVerified = true } },
                new { persona = "admin", profile = new { isComplete = true, user = member, email = member.email, emailVerified = true } }
            },
            sponsors = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(document);
    }

    private static void SetTournamentStatus(MockBackendStore store, TournamentStatus status)
    {
        var document = (MockBackendDocument)typeof(MockBackendStore)
            .GetField("_document", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store)!;
        document.Tournaments.Single(tournament => tournament.Id == TournamentId).Status = status;
    }

    private sealed class Fixture(string root, MockBackendStore store) : IDisposable
    {
        public MockBackendStore Store { get; } = store;

        public void Dispose()
        {
            if(Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = typeof(MockRegistrationParityTests).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
