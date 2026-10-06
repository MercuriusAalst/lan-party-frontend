using Mercurius.LAN.Web.Components.Pages.Tournaments;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Models.Tournaments;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentLifecycleActionCriteriaTests
{
    [Fact]
    public void LeaderboardFinishingRequiresARankedResultForItsMetric()
    {
        var leaderboard = new PublicLeaderboardDTO
        {
            RankingMetric = LeaderboardRankingMetric.HighestScore,
            Rows = [new LeaderboardRowDTO { Rank = 0, Score = 10 }]
        };

        Assert.False(TournamentDetail.HasRankedLeaderboardResult(leaderboard));

        leaderboard.Rows = [new LeaderboardRowDTO { Rank = 1 }];
        Assert.False(TournamentDetail.HasRankedLeaderboardResult(leaderboard));

        leaderboard.Rows = [new LeaderboardRowDTO { Rank = 1, Score = 10 }];
        Assert.True(TournamentDetail.HasRankedLeaderboardResult(leaderboard));

        leaderboard.RankingMetric = LeaderboardRankingMetric.FastestTime;
        leaderboard.Rows = [new LeaderboardRowDTO { Rank = 1, DurationMilliseconds = 1000 }];
        Assert.True(TournamentDetail.HasRankedLeaderboardResult(leaderboard));
    }

    [Fact]
    public void LifecycleActionsMatchSupportedBackendStatesAndPrerequisites()
    {
        var tournament = new TournamentExtended
        {
            Status = TournamentStatus.Scheduled,
            BracketType = BracketType.SingleElimination,
            ParticipationMode = ParticipationMode.Individual
        };

        Assert.False(TournamentDetail.CanStartTournament(tournament));
        Assert.Equal("Feature.tournaments.startRequiresRegistrations", TournamentDetail.GetStartBlockReasonKey(tournament));

        tournament.Registrations =
        [
            CreateRegistration(TournamentRegistrationKind.Individual),
            CreateRegistration(TournamentRegistrationKind.Individual, TournamentRegistrationStatus.PendingConfirmation)
        ];
        Assert.False(TournamentDetail.CanStartTournament(tournament));

        tournament.Registrations =
        [
            CreateRegistration(TournamentRegistrationKind.Individual),
            CreateRegistration(TournamentRegistrationKind.Team)
        ];
        Assert.False(TournamentDetail.CanStartTournament(tournament));

        tournament.Registrations =
        [
            CreateRegistration(TournamentRegistrationKind.Individual),
            CreateRegistration(TournamentRegistrationKind.Individual)
        ];
        Assert.True(TournamentDetail.CanStartTournament(tournament));
        Assert.True(TournamentDetail.CanCancelTournament(tournament));
        Assert.True(TournamentDetail.CanDeleteTournament(tournament));
        Assert.False(TournamentDetail.CanResetTournament(tournament));

        tournament.BracketType = BracketType.Swiss;
        Assert.False(TournamentDetail.CanStartTournament(tournament));
        Assert.Equal("Feature.tournaments.unsupportedSwissBracket", TournamentDetail.GetStartBlockReasonKey(tournament));

        tournament.BracketType = BracketType.Leaderboard;
        tournament.Registrations = [];
        Assert.True(TournamentDetail.CanStartTournament(tournament));

        tournament.Status = TournamentStatus.InProgress;
        Assert.False(TournamentDetail.CanStartTournament(tournament));
        Assert.False(TournamentDetail.CanDeleteTournament(tournament));
        Assert.False(TournamentDetail.CanResetTournament(tournament));
        Assert.True(TournamentDetail.CanFinishTournament(tournament, hasLeaderboardResults: true));
        Assert.False(TournamentDetail.CanFinishTournament(tournament, hasLeaderboardResults: false));

        tournament.BracketType = BracketType.Swiss;
        Assert.True(TournamentDetail.CanFinishTournament(tournament, hasLeaderboardResults: false));

        tournament.BracketType = BracketType.SingleElimination;
        Assert.True(TournamentDetail.CanFinishTournament(tournament, hasLeaderboardResults: false));

        tournament.Status = TournamentStatus.Completed;
        Assert.False(TournamentDetail.CanFinishTournament(tournament, hasLeaderboardResults: true));
        Assert.False(TournamentDetail.CanCancelTournament(tournament));
        Assert.True(TournamentDetail.CanResetTournament(tournament));
        Assert.True(TournamentDetail.CanDeleteTournament(tournament));

        tournament.Status = TournamentStatus.Canceled;
        Assert.False(TournamentDetail.CanFinishTournament(tournament, hasLeaderboardResults: true));
        Assert.False(TournamentDetail.CanCancelTournament(tournament));
        Assert.True(TournamentDetail.CanResetTournament(tournament));
    }

    private static PublicTournamentRegistrationDTO CreateRegistration(
        TournamentRegistrationKind kind,
        TournamentRegistrationStatus status = TournamentRegistrationStatus.Active) =>
        new()
        {
            Kind = kind,
            Status = status
        };
}
