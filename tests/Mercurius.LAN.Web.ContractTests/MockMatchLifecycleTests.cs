using System.Text.Json;
using System.Text.Json.Serialization;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Mock;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Options;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MockMatchLifecycleTests
{
    private static readonly Guid FeaturedTournamentId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    private static readonly Guid FeaturedGrandFinalId = Guid.Parse("31111111-1111-1111-1111-111111111115");
    private static readonly Guid IndividualProfileFixtureTournamentId = Guid.Parse("91111111-1111-1111-1111-111111111111");
    private static readonly Guid IndividualProfileFixtureOpponentId = Guid.Parse("91111111-1111-1111-1111-111111111113");
    private static readonly Guid FeaturedLowerBracketFinalId = Guid.Parse("31111111-1111-1111-1111-111111111114");
    private static readonly Guid ReplayTournamentId = Guid.Parse("51111111-1111-1111-1111-111111111111");
    private static readonly Guid ReplayMatchId = Guid.Parse("51111111-1111-1111-1111-111111111112");
    private static readonly Guid ReplayTeam1Id = Guid.Parse("52111111-1111-1111-1111-111111111111");
    private static readonly Guid ReplayTeam2Id = Guid.Parse("52111111-1111-1111-1111-111111111112");
    private static readonly Guid ReplayUserId = Guid.Parse("53111111-1111-1111-1111-111111111111");
    private static readonly Guid ReplayAdminId = Guid.Parse("53111111-1111-1111-1111-111111111112");

    [Fact]
    public void MockAdminForfeitAndReverseKeepExplicitLifecycleState()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));

        var initial = store.GetMatchActionState("admin", FeaturedGrandFinalId);
        Assert.Equal(MatchLifecycleState.AwaitingEndedConfirmation, initial.Match.LifecycleState);
        Assert.False(initial.CanForfeit);
        Assert.True(initial.CanForceForfeit);
        Assert.False(initial.CanResolve);
        Assert.Equal("match_not_disputed", initial.ResolveBlockedReason);

        var forfeited = store.ForfeitMatch(
            "admin",
            FeaturedGrandFinalId,
            new ForfeitMatchDTO { Participant = MatchParticipantSide.Participant1 });

        Assert.Equal(MatchLifecycleState.Forfeited, forfeited.LifecycleState);
        Assert.Equal(MatchResultKind.Forfeit, forfeited.ResultKind);
        Assert.Equal(1, forfeited.ForfeitedParticipantNumber);

        var reversed = store.ReverseMatch("admin", FeaturedGrandFinalId);

        Assert.Equal(MatchLifecycleState.Reversed, reversed.LifecycleState);
        Assert.Null(reversed.ResultKind);
        Assert.Null(reversed.Participant1Score);
        Assert.Null(reversed.Participant2Score);
        Assert.False(store.GetMatchActionState("admin", FeaturedGrandFinalId).CanReverse);
    }

    [Fact]
    public void MockReverseClearsWinnerAndLoserFromBothDownstreamPaths()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));

        store.SetTournamentLifecycleState(FeaturedTournamentId, Mercurius.LAN.Web.Models.Tournaments.TournamentStatus.Canceled);
        store.SetTournamentLifecycleState(FeaturedTournamentId, Mercurius.LAN.Web.Models.Tournaments.TournamentStatus.Scheduled);
        store.SetTournamentLifecycleState(FeaturedTournamentId, Mercurius.LAN.Web.Models.Tournaments.TournamentStatus.InProgress);
        var before = store.GetTournament(FeaturedTournamentId)!;
        var source = before.Matches
            .Where(match =>
                !match.IsLowerBracketMatch &&
                match.RoundNumber == 1 &&
                match.TeamParticipant1Id.HasValue &&
                match.TeamParticipant2Id.HasValue &&
                match.WinnerNextMatchId.HasValue &&
                match.LoserNextMatchId.HasValue)
            .OrderBy(match => match.MatchNumber)
            .First();
        var winnerNextId = source.WinnerNextMatchId ?? throw new InvalidOperationException("Fixture winner path is missing.");
        var loserNextId = source.LoserNextMatchId ?? throw new InvalidOperationException("Fixture loser path is missing.");
        var participant1Id = source.TeamParticipant1Id;
        var participant2Id = source.TeamParticipant2Id;

        store.ForfeitMatch(
            "admin",
            source.Id,
            new ForfeitMatchDTO { Participant = MatchParticipantSide.Participant1 });
        var actionState = store.GetMatchActionState("admin", source.Id);
        Assert.True(actionState.CanReverse);

        store.ReverseMatch("admin", source.Id);
        var after = store.GetTournament(FeaturedTournamentId)!;
        var winnerNext = after.Matches.Single(match => match.Id == winnerNextId);
        var loserNext = after.Matches.Single(match => match.Id == loserNextId);

        Assert.DoesNotContain(participant1Id, new[] { winnerNext.TeamParticipant1Id, winnerNext.TeamParticipant2Id });
        Assert.DoesNotContain(participant2Id, new[] { winnerNext.TeamParticipant1Id, winnerNext.TeamParticipant2Id });
        Assert.DoesNotContain(participant1Id, new[] { loserNext.TeamParticipant1Id, loserNext.TeamParticipant2Id });
        Assert.DoesNotContain(participant2Id, new[] { loserNext.TeamParticipant1Id, loserNext.TeamParticipant2Id });
    }

    [Fact]
    public void MockReversalClearsProvenancedFeaturedGrandFinalAssignment()
    {
        var store = CreateFeaturedStore();

        var actionState = store.GetMatchActionState("admin", FeaturedLowerBracketFinalId);

        Assert.True(actionState.CanReverse);
        store.ReverseMatch("admin", FeaturedLowerBracketFinalId);
        var final = store.GetTournament(FeaturedTournamentId)!.Matches.Single(match => match.Id == FeaturedGrandFinalId);
        Assert.Null(final.TeamParticipant2Id);
        Assert.Null(final.Participant2SourceMatchId);
    }
    [Fact]
    public void MockReversalStillRejectsUnprovenancedDownstreamAssignment()
    {
        var store = CreateFeaturedStore();
        var document = (MockBackendDocument)typeof(MockBackendStore)
            .GetField("_document", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(store)!;
        document.Tournaments.Single(tournament => tournament.Id == FeaturedTournamentId)
            .Matches.Single(match => match.Id == FeaturedGrandFinalId).Participant2SourceMatchId = null;

        Assert.False(store.GetMatchActionState("admin", FeaturedLowerBracketFinalId).CanReverse);
        var exception = Assert.Throws<InvalidOperationException>(() => store.ReverseMatch("admin", FeaturedLowerBracketFinalId));
        Assert.StartsWith("match_reversal_blocked:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MockReversedMatchCanReplayAndSecondReversalClearsEndTime()
    {
        var store = CreateReplayStore();

        var reversed = store.ReverseMatch("admin", ReplayMatchId);

        Assert.Equal(MatchLifecycleState.Reversed, reversed.LifecycleState);
        Assert.Equal(default, reversed.EndTime);
        Assert.True(store.GetMatchActionState("user", ReplayMatchId).CanConfirmEnded);

        store.ConfirmMatchEnded("user", ReplayMatchId);
        store.ConfirmMatchEnded("admin", ReplayMatchId);
        store.SubmitMatchScore(
            "user",
            ReplayMatchId,
            new SubmitMatchScoreDTO { Participant1Score = 1, Participant2Score = 0 });
        var completed = store.SubmitMatchScore(
            "admin",
            ReplayMatchId,
            new SubmitMatchScoreDTO { Participant1Score = 1, Participant2Score = 0 });

        Assert.Equal(MatchLifecycleState.Completed, completed.LifecycleState);
        Assert.NotEqual(default, completed.EndTime);

        var reversedAgain = store.ReverseMatch("admin", ReplayMatchId);

        Assert.Equal(MatchLifecycleState.Reversed, reversedAgain.LifecycleState);
        Assert.Equal(default, reversedAgain.EndTime);
        Assert.True(store.GetMatchActionState("user", ReplayMatchId).CanConfirmEnded);
    }

    [Fact]
    public void MockForfeitClearsPendingScoreAndCorrectionDeadlines()
    {
        var scoreConfirmationStore = CreateReplayStore();

        scoreConfirmationStore.ReverseMatch("admin", ReplayMatchId);
        scoreConfirmationStore.ConfirmMatchEnded("user", ReplayMatchId);
        scoreConfirmationStore.ConfirmMatchEnded("admin", ReplayMatchId);
        scoreConfirmationStore.SubmitMatchScore(
            "user",
            ReplayMatchId,
            new SubmitMatchScoreDTO { Participant1Score = 1, Participant2Score = 0 });

        var scoreConfirmation = scoreConfirmationStore.GetMatch(ReplayMatchId);
        Assert.NotNull(scoreConfirmation.ScoreConfirmationDeadlineUtc);
        Assert.Null(scoreConfirmation.CorrectionDeadlineUtc);

        var scoreConfirmationForfeited = scoreConfirmationStore.ForfeitMatch(
            "admin",
            ReplayMatchId,
            new ForfeitMatchDTO { Participant = MatchParticipantSide.Participant1 });

        Assert.Equal(MatchLifecycleState.Forfeited, scoreConfirmationForfeited.LifecycleState);
        Assert.Null(scoreConfirmationForfeited.ScoreConfirmationDeadlineUtc);
        Assert.Null(scoreConfirmationForfeited.CorrectionDeadlineUtc);

        var disputedStore = CreateReplayStore();
        disputedStore.ReverseMatch("admin", ReplayMatchId);
        disputedStore.ConfirmMatchEnded("user", ReplayMatchId);
        disputedStore.ConfirmMatchEnded("admin", ReplayMatchId);
        disputedStore.SubmitMatchScore(
            "user",
            ReplayMatchId,
            new SubmitMatchScoreDTO { Participant1Score = 1, Participant2Score = 0 });
        disputedStore.SubmitMatchScore(
            "admin",
            ReplayMatchId,
            new SubmitMatchScoreDTO { Participant1Score = 0, Participant2Score = 1 });

        var disputed = disputedStore.GetMatch(ReplayMatchId);
        Assert.Null(disputed.ScoreConfirmationDeadlineUtc);
        Assert.NotNull(disputed.CorrectionDeadlineUtc);

        var disputedForfeited = disputedStore.ForfeitMatch(
            "admin",
            ReplayMatchId,
            new ForfeitMatchDTO { Participant = MatchParticipantSide.Participant1 });

        Assert.Equal(MatchLifecycleState.Forfeited, disputedForfeited.LifecycleState);
        Assert.Null(disputedForfeited.ScoreConfirmationDeadlineUtc);
        Assert.Null(disputedForfeited.CorrectionDeadlineUtc);
    }

    [Fact]
    public void MockResultPropagationMirrorsBackendOrientationAndReplacesSourceOwnedSlots()
    {
        var tournament = new TournamentExtended
        {
            Id = Guid.NewGuid(),
            ParticipationMode = ParticipationMode.Team
        };
        var upperOddSource = CreateTeamResultMatch(tournament.Id, roundNumber: 1, matchNumber: 1);
        var upperOddWinnerTarget = CreateTeamTarget(
            upperOddSource.Id,
            participant1Id: Guid.NewGuid(),
            participant2Id: Guid.NewGuid(),
            replaceParticipant1: true);
        var upperOddLoserTarget = CreateTeamTarget(
            upperOddSource.Id,
            participant1Id: Guid.NewGuid(),
            participant2Id: Guid.NewGuid(),
            replaceParticipant1: true);
        upperOddSource.WinnerNextMatchId = upperOddWinnerTarget.Id;
        upperOddSource.LoserNextMatchId = upperOddLoserTarget.Id;

        var upperEvenSource = CreateTeamResultMatch(tournament.Id, roundNumber: 1, matchNumber: 2);
        var upperEvenWinnerTarget = CreateTeamTarget(
            upperEvenSource.Id,
            participant1Id: Guid.NewGuid(),
            participant2Id: Guid.NewGuid(),
            replaceParticipant1: false);
        var upperEvenLoserTarget = CreateTeamTarget(
            upperEvenSource.Id,
            participant1Id: Guid.NewGuid(),
            participant2Id: Guid.NewGuid(),
            replaceParticipant1: false);
        upperEvenSource.WinnerNextMatchId = upperEvenWinnerTarget.Id;
        upperEvenSource.LoserNextMatchId = upperEvenLoserTarget.Id;

        var lowerSource = CreateTeamResultMatch(tournament.Id, roundNumber: 2, matchNumber: 1, isLowerBracketMatch: true);
        var lowerWinnerTarget = CreateTeamTarget(
            lowerSource.Id,
            participant1Id: Guid.NewGuid(),
            participant2Id: null,
            isLowerBracketMatch: true);
        var lowerLoserTarget = CreateTeamTarget(
            lowerSource.Id,
            participant1Id: Guid.NewGuid(),
            participant2Id: Guid.NewGuid(),
            replaceParticipant1: true,
            isLowerBracketMatch: true);
        lowerSource.WinnerNextMatchId = lowerWinnerTarget.Id;
        lowerSource.LoserNextMatchId = lowerLoserTarget.Id;

        tournament.Matches =
        [
            upperOddSource,
            upperOddWinnerTarget,
            upperOddLoserTarget,
            upperEvenSource,
            upperEvenWinnerTarget,
            upperEvenLoserTarget,
            lowerSource,
            lowerWinnerTarget,
            lowerLoserTarget
        ];

        MockBackendStore.PropagateMockResult(tournament, upperOddSource);
        MockBackendStore.PropagateMockResult(tournament, upperEvenSource);
        MockBackendStore.PropagateMockResult(tournament, lowerSource);

        Assert.Equal(upperOddSource.TeamWinnerId, upperOddWinnerTarget.TeamParticipant1Id);
        Assert.Equal(upperOddSource.Id, upperOddWinnerTarget.Participant1SourceMatchId);
        Assert.Equal(upperOddSource.TeamLoserId, upperOddLoserTarget.TeamParticipant1Id);
        Assert.Equal(upperOddSource.Id, upperOddLoserTarget.Participant1SourceMatchId);
        Assert.Equal(upperEvenSource.TeamWinnerId, upperEvenWinnerTarget.TeamParticipant2Id);
        Assert.Equal(upperEvenSource.Id, upperEvenWinnerTarget.Participant2SourceMatchId);
        Assert.Equal(upperEvenSource.TeamLoserId, upperEvenLoserTarget.TeamParticipant2Id);
        Assert.Equal(upperEvenSource.Id, upperEvenLoserTarget.Participant2SourceMatchId);
        Assert.Equal(lowerSource.TeamWinnerId, lowerWinnerTarget.TeamParticipant2Id);
        Assert.Equal(lowerSource.Id, lowerWinnerTarget.Participant2SourceMatchId);
        Assert.Equal(lowerSource.TeamLoserId, lowerLoserTarget.TeamParticipant1Id);
        Assert.Equal(lowerSource.Id, lowerLoserTarget.Participant1SourceMatchId);
    }

    [Fact]
    public void MockAnonymousMatchStateUsesPublicProjectionWithoutPrivateReports()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));

        var state = store.GetMatchActionState("anonymous", FeaturedGrandFinalId);

        Assert.Null(state.AuthorizedParticipant);
        Assert.Null(state.Participant1ReportedScore1);
        Assert.Null(state.Participant1ReportedScore2);
        Assert.Null(state.Participant2ReportedScore1);
        Assert.Null(state.Participant2ReportedScore2);

        var publicMatch = store.GetMatch(FeaturedGrandFinalId);
        Assert.Equal(state.Match.Participant1Score, publicMatch.Participant1Score);
        Assert.Equal(state.Match.Participant2Score, publicMatch.Participant2Score);
        Assert.Null(publicMatch.Participant1ReportedScore1);
        Assert.Null(publicMatch.Participant2ReportedScore1);

        Assert.Null(store.GetTournament(FeaturedTournamentId)!.AssignedAdminUserId);
    }

    [Fact]
    public async Task MockAnonymousLoginAndUnauthenticatedRequestsStayAnonymousInTournamentService()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));

        var anonymousPrincipal = DependencyExtensions.BuildMockPrincipal("anonymous", store);
        Assert.False(anonymousPrincipal.Identity?.IsAuthenticated);
        Assert.Equal("anonymous", anonymousPrincipal.FindFirst("mock_persona")?.Value);

        var loginContext = new DefaultHttpContext { User = anonymousPrincipal };
        var loginService = new MockTournamentService(store, new HttpContextAccessor { HttpContext = loginContext });
        var loginState = await loginService.GetMatchActionStateAsync(FeaturedGrandFinalId);

        var unauthenticatedContext = new DefaultHttpContext();
        var unauthenticatedService = new MockTournamentService(
            store,
            new HttpContextAccessor { HttpContext = unauthenticatedContext });
        var unauthenticatedState = await unauthenticatedService.GetMatchActionStateAsync(FeaturedGrandFinalId);

        Assert.Null(loginState.AuthorizedParticipant);
        Assert.Null(loginState.Participant1ReportedScore1);
        Assert.Null(loginState.Participant2ReportedScore1);
        Assert.Null(unauthenticatedState.AuthorizedParticipant);
        Assert.Null(unauthenticatedState.Participant1ReportedScore1);
        Assert.Null(unauthenticatedState.Participant2ReportedScore1);
    }

    [Fact]
    public void MockPrivateReportsMatchLiveAssignedAdminVisibilityRules()
    {
        var adminId = Guid.Parse("41111111-1111-1111-1111-111111111121");
        var otherAdminId = Guid.NewGuid();

        Assert.True(MockBackendStore.CanViewPrivateReports("admin", adminId, adminId, false));
        Assert.False(MockBackendStore.CanViewPrivateReports("admin", adminId, otherAdminId, false));
        Assert.True(MockBackendStore.CanViewPrivateReports("admin", null, otherAdminId, false));
        Assert.True(MockBackendStore.CanViewPrivateReports("user", adminId, otherAdminId, true));
        Assert.False(MockBackendStore.CanViewPrivateReports("user", adminId, otherAdminId, false));
    }

    [Fact]
    public void MockPublicProfilesExposeBoundedLifecycleAwareMatchSummaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));

        var userSummaries = store.GetPublicUserMatchSummaries("alpha1");
        Assert.NotNull(userSummaries);
        Assert.Equal(2, userSummaries!.PreviousMatches.Count);
        Assert.Contains(userSummaries.PreviousMatches, summary => summary.TournamentName == "Rocket League");
        Assert.Single(userSummaries.UpcomingMatches);
        Assert.Equal("Valorant Gamma Grid", userSummaries.UpcomingMatches[0].OpponentDisplayName);
        Assert.Equal(MatchLifecycleState.AwaitingEndedConfirmation, userSummaries.UpcomingMatches[0].LifecycleState);
        Assert.NotNull(userSummaries.PreviousMatches[0].ParticipantScore);

        var teamSummaries = store.GetPublicTeamMatchSummaries("Valorant Team Alpha");
        Assert.NotNull(teamSummaries);
        Assert.Single(teamSummaries!.PreviousMatches);
        Assert.Single(teamSummaries.UpcomingMatches);
        Assert.Equal(userSummaries.UpcomingMatches[0].MatchId, teamSummaries.UpcomingMatches[0].MatchId);

        var emptySummaries = store.GetPublicUserMatchSummaries("track1");
        Assert.NotNull(emptySummaries);
        Assert.Equal("Rocket League", Assert.Single(emptySummaries!.PreviousMatches).TournamentName);
        Assert.Empty(emptySummaries.UpcomingMatches);

        var individualSummaries = store.GetPublicUserMatchSummaries("solo1");
        Assert.NotNull(individualSummaries);
        Assert.Single(individualSummaries!.PreviousMatches);
        Assert.Single(individualSummaries.UpcomingMatches);
        Assert.Equal("solo2", individualSummaries.UpcomingMatches[0].OpponentDisplayName);
        Assert.Equal(2, individualSummaries.PreviousMatches[0].ParticipantScore);
        Assert.Equal(1, individualSummaries.PreviousMatches[0].OpponentScore);

        Assert.Null(store.GetPublicUserMatchSummaries("missing-user"));
    }

    [Fact]
    public void MockPublicProfileSummariesRetainHistoricalOpponentSnapshotAfterRegistrationRemoval()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));

        store.RemoveTournamentUserRegistrationAsAdmin(
            IndividualProfileFixtureTournamentId,
            IndividualProfileFixtureOpponentId,
            "Historical match snapshot regression");

        var summaries = store.GetPublicUserMatchSummaries("solo1");

        Assert.NotNull(summaries);
        Assert.Equal("solo2", Assert.Single(summaries!.PreviousMatches).OpponentDisplayName);
        Assert.Equal("solo2", Assert.Single(summaries.UpcomingMatches).OpponentDisplayName);
    }

    [Fact]
    public void MockPublicProfileSummariesPreferCurrentPublicOpponentLabelsAfterRename()
    {
        var store = CreateRenamedOpponentStore();

        store.UpdateCurrentProfile("user", new UpdateUserProfileRequest
        {
            Username = "current-opponent",
            Firstname = "Current",
            Lastname = "Opponent"
        });
        var opponentTeam = Assert.Single(store.GetTeams(1, 100).Where(team => team.Name == "Valorant Gamma Grid"));
        store.UpdateTeam(opponentTeam.Id, new UpdateTeamDTO
        {
            Name = "Current Gamma Grid"
        });

        var userSummaries = store.GetPublicUserMatchSummaries("solo1");
        var teamSummaries = store.GetPublicTeamMatchSummaries("Valorant Team Alpha");

        Assert.Equal("current-opponent", Assert.Single(userSummaries!.PreviousMatches).OpponentDisplayName);
        Assert.Equal("current-opponent", Assert.Single(userSummaries.UpcomingMatches).OpponentDisplayName);
        Assert.Equal("Current Gamma Grid", Assert.Single(teamSummaries!.UpcomingMatches).OpponentDisplayName);
    }

    [Fact]
    public void MockRemovingFinalTeamRegistrationDoesNotRepopulatePublicProjection()
    {
        var store = CreateFeaturedStore();
        var teamIds = store.GetTournament(FeaturedTournamentId)!.Registrations
            .Where(registration =>
                registration.Kind == TournamentRegistrationKind.Team &&
                registration.Status == TournamentRegistrationStatus.Active)
            .Select(registration => registration.Team!.Id)
            .ToList();

        Assert.NotEmpty(teamIds);
        foreach(var teamId in teamIds)
            store.RemoveTournamentTeamRegistrationAsAdmin(
                FeaturedTournamentId,
                teamId,
                "Final registration projection regression");

        Assert.Empty(store.GetTournament(FeaturedTournamentId)!.Registrations);

        var teamProfile = store.GetPublicTeamByName("Pixel Pushers");
        Assert.NotNull(teamProfile);
        Assert.Empty(teamProfile!.Tournaments);

        var summaries = store.GetPublicTeamMatchSummaries("Pixel Pushers");
        Assert.NotNull(summaries);
        Assert.Empty(summaries!.PreviousMatches);
        Assert.Empty(summaries.UpcomingMatches);
    }

    [Fact]
    public void MockDeletedTeamIsNotPublicAndAReusedNameDoesNotInheritHistory()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));

        var originalTeam = Assert.Single(store.GetTeams(1, 100).Where(team => team.Name == "Team Alpha"));
        Assert.NotNull(store.GetPublicTeamByName("Team Alpha"));
        Assert.NotNull(store.GetPublicTeamMatchSummaries("Team Alpha"));

        store.DeleteTeam(originalTeam.Id);

        Assert.Null(store.GetPublicTeamByName("Team Alpha"));
        Assert.Null(store.GetPublicTeamMatchSummaries("Team Alpha"));

        var captain = store.GetPublicUserByUsername("alpha1");
        Assert.NotNull(captain);
        var replacement = store.CreateTeam(new CreateTeamDTO
        {
            Name = "Team Alpha",
            CaptainUserId = Guid.Parse("41111111-1111-1111-1111-111111111111")
        });

        Assert.NotNull(store.GetPublicTeamByName("Team Alpha"));
        var replacementSummaries = store.GetPublicTeamMatchSummaries(replacement.Name);
        Assert.NotNull(replacementSummaries);
        Assert.Empty(replacementSummaries!.PreviousMatches);
        Assert.Empty(replacementSummaries.UpcomingMatches);
    }

    [Fact]
    public async Task MockServicesUseAuthenticationStateWhenInteractiveCircuitHasNoHttpContext()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));
        var adminPrincipal = DependencyExtensions.BuildMockPrincipal("admin", store);
        var authenticationStateProvider = new FixedAuthenticationStateProvider(adminPrincipal);
        var accessor = new HttpContextAccessor();

        var tournamentService = new MockTournamentService(store, accessor, authenticationStateProvider);
        var actionState = await tournamentService.GetMatchActionStateAsync(FeaturedGrandFinalId);

        Assert.True(actionState.CanForceForfeit);

        var userPrincipal = DependencyExtensions.BuildMockPrincipal("user", store);
        var userClient = new MockUserClient(
            store,
            accessor,
            new FixedAuthenticationStateProvider(userPrincipal));
        var profile = await userClient.GetCurrentUserProfileAsync();

        Assert.Equal("mockuser", profile.User?.Username);

        var teamService = new MockTeamService(
            store,
            accessor,
            new FixedAuthenticationStateProvider(userPrincipal));
        var teamSummary = await teamService.GetCurrentUserTeamSummaryAsync();

        Assert.Contains(profile.User!.Id, teamSummary.CaptainedTeams.Select(team => team.CaptainUserId));
    }

    private static MockBackendStore CreateFeaturedStore()
    {
        var repositoryRoot = FindRepositoryRoot();
        return new MockBackendStore(
            new TestHostEnvironment(repositoryRoot),
            Microsoft.Extensions.Options.Options.Create(new MockBackendOptions
            {
                DataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json")
            }));
    }

    private static MockBackendStore CreateRenamedOpponentStore()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dataFilePath = Path.Combine(repositoryRoot, "src", "Mercurius.LAN.Web", "MockData.Local", "backend.json");
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        var document = JsonSerializer.Deserialize<MockBackendDocument>(File.ReadAllText(dataFilePath), serializerOptions)
            ?? throw new InvalidOperationException("Failed to load the mock backend fixture.");
        var profile = document.Profiles.Single(candidate => candidate.Persona == "user");
        var opponent = profile.Profile.User ?? throw new InvalidOperationException("Mock user profile is missing.");
        opponent.Id = IndividualProfileFixtureOpponentId;
        opponent.Username = "solo2";
        opponent.Firstname = "Omar";
        opponent.Lastname = "Opponent";
        opponent.DisplayName = "solo2";

        return CreateSerializedStore(document, "mercurius-lan-current-labels");
    }

    private static MockBackendStore CreateSerializedStore(MockBackendDocument document, string directoryPrefix)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"{directoryPrefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var dataFilePath = Path.Combine(temporaryDirectory, "backend.json");
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        File.WriteAllText(dataFilePath, JsonSerializer.Serialize(document, serializerOptions));

        try
        {
            return new MockBackendStore(
                new TestHostEnvironment(temporaryDirectory),
                Microsoft.Extensions.Options.Options.Create(new MockBackendOptions { DataFilePath = "backend.json" }));
        }
        finally
        {
            File.Delete(dataFilePath);
            Directory.Delete(temporaryDirectory);
        }
    }

    private static MockBackendStore CreateReplayStore()
    {
        var now = DateTime.UtcNow;
        var user = new UserProfileDTO
        {
            Id = ReplayUserId,
            Username = "replay-user",
            Firstname = "Replay",
            Lastname = "User",
            Email = "replay-user@example.test",
            EmailVerified = true,
            DisplayName = "Replay User",
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        };
        var admin = new UserProfileDTO
        {
            Id = ReplayAdminId,
            Username = "replay-admin",
            Firstname = "Replay",
            Lastname = "Admin",
            Email = "replay-admin@example.test",
            EmailVerified = true,
            DisplayName = "Replay Admin",
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        };
        var team1 = new Team
        {
            Id = ReplayTeam1Id,
            Name = "Replay One",
            CaptainUserId = ReplayUserId,
            Members = []
        };
        var team2 = new Team
        {
            Id = ReplayTeam2Id,
            Name = "Replay Two",
            CaptainUserId = ReplayAdminId,
            Members = []
        };
        var match = new Match
        {
            Id = ReplayMatchId,
            TournamentId = ReplayTournamentId,
            ParticipationMode = ParticipationMode.Team,
            BracketType = BracketType.SingleElimination,
            Format = TournamentFormat.BestOf1,
            RoundNumber = 1,
            MatchNumber = 1,
            TeamParticipant1Id = ReplayTeam1Id,
            TeamParticipant2Id = ReplayTeam2Id,
            TeamWinnerId = ReplayTeam1Id,
            TeamLoserId = ReplayTeam2Id,
            Participant1Score = 1,
            Participant2Score = 0,
            LifecycleState = MatchLifecycleState.Completed,
            ResultKind = MatchResultKind.Score,
            Participant1Ended = true,
            Participant2Ended = true,
            StartTime = now.AddHours(-1),
            EndTime = now.AddMinutes(-30),
            EstimatedStartTime = now.AddHours(-1),
            EstimatedEndTime = now.AddMinutes(-30),
            ResultVersion = 1
        };
        var tournament = new TournamentExtended
        {
            Id = ReplayTournamentId,
            Name = "Replay Tournament",
            StartTime = now.AddHours(-2),
            EndTime = now.AddHours(2),
            PlannedStartTime = now.AddHours(-2),
            EstimatedEndTime = now.AddHours(2),
            Status = TournamentStatus.InProgress,
            BracketType = BracketType.SingleElimination,
            Format = TournamentFormat.BestOf1,
            FinalsFormat = TournamentFormat.BestOf1,
            ParticipationMode = ParticipationMode.Team,
            Teams = [team1, team2],
            Matches = [match]
        };
        var document = new MockBackendDocument
        {
            Tournaments = [tournament],
            Teams = [team1, team2],
            Profiles =
            [
                new MockProfileRecord
                {
                    Persona = "user",
                    Profile = new CurrentUserProfileResponse(true, user, user.Email, true)
                },
                new MockProfileRecord
                {
                    Persona = "admin",
                    Profile = new CurrentUserProfileResponse(true, admin, admin.Email, true)
                }
            ]
        };

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"mercurius-lan-replay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var dataFilePath = Path.Combine(temporaryDirectory, "backend.json");
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        File.WriteAllText(dataFilePath, JsonSerializer.Serialize(document, serializerOptions));

        try
        {
            return new MockBackendStore(
                new TestHostEnvironment(temporaryDirectory),
                Microsoft.Extensions.Options.Options.Create(new MockBackendOptions { DataFilePath = "backend.json" }));
        }
        finally
        {
            File.Delete(dataFilePath);
            Directory.Delete(temporaryDirectory);
        }
    }

    private static Match CreateTeamResultMatch(Guid tournamentId, int roundNumber, int matchNumber, bool isLowerBracketMatch = false)
    {
        var participant1Id = Guid.NewGuid();
        var participant2Id = Guid.NewGuid();
        return new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            ParticipationMode = ParticipationMode.Team,
            BracketType = BracketType.DoubleElimination,
            Format = TournamentFormat.BestOf1,
            RoundNumber = roundNumber,
            MatchNumber = matchNumber,
            IsLowerBracketMatch = isLowerBracketMatch,
            TeamParticipant1Id = participant1Id,
            TeamParticipant2Id = participant2Id,
            TeamWinnerId = participant1Id,
            TeamLoserId = participant2Id,
            LifecycleState = MatchLifecycleState.Completed,
            ResultKind = MatchResultKind.Score
        };
    }

    private static Match CreateTeamTarget(
        Guid sourceMatchId,
        Guid? participant1Id,
        Guid? participant2Id,
        bool replaceParticipant1 = false,
        bool isLowerBracketMatch = false)
    {
        return new Match
        {
            Id = Guid.NewGuid(),
            ParticipationMode = ParticipationMode.Team,
            BracketType = BracketType.DoubleElimination,
            Format = TournamentFormat.BestOf1,
            IsLowerBracketMatch = isLowerBracketMatch,
            TeamParticipant1Id = participant1Id,
            TeamParticipant2Id = participant2Id,
            Participant1SourceMatchId = replaceParticipant1 ? sourceMatchId : null,
            Participant2SourceMatchId = replaceParticipant1 ? null : sourceMatchId
        };
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

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(principal);

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(_state);
    }
}
