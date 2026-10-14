using System.Net;
using System.Net.Http;
using System.Text;
using Mercurius.LAN.Web.Components.Pages.Tournaments;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Matches.DetailView;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Tournaments;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class MatchLifecycleUiStateTests
{
    [Theory]
    [InlineData(TournamentFormat.BestOf1, 1, 0, true)]
    [InlineData(TournamentFormat.BestOf1, 0, 1, true)]
    [InlineData(TournamentFormat.BestOf3, 2, 1, true)]
    [InlineData(TournamentFormat.BestOf3, 1, 2, true)]
    [InlineData(TournamentFormat.BestOf5, 3, 2, true)]
    [InlineData(TournamentFormat.BestOf5, 2, 3, true)]
    [InlineData(TournamentFormat.BestOf1, 0, 0, false)]
    [InlineData(TournamentFormat.BestOf1, 1, 1, false)]
    [InlineData(TournamentFormat.BestOf1, 2, 0, false)]
    [InlineData(TournamentFormat.BestOf3, 2, 2, false)]
    [InlineData(TournamentFormat.BestOf3, 1, 0, false)]
    [InlineData(TournamentFormat.BestOf3, 3, 0, false)]
    [InlineData(TournamentFormat.BestOf5, 3, 3, false)]
    [InlineData(TournamentFormat.BestOf5, 2, 1, false)]
    [InlineData(TournamentFormat.BestOf5, 4, 0, false)]
    [InlineData(TournamentFormat.BestOf3, -1, 0, false)]
    [InlineData(TournamentFormat.BestOf5, 0, -1, false)]
    [InlineData(TournamentFormat.BestOf3, null, 0, false)]
    [InlineData(TournamentFormat.BestOf3, 2, null, false)]
    [InlineData(TournamentFormat.BestOf3, null, null, false)]
    public void SeriesScoreValidation_RequiresADecisiveScoreForTheConfiguredFormat(
        TournamentFormat format,
        int? participant1Score,
        int? participant2Score,
        bool expected)
    {
        Assert.Equal(expected, TournamentMatchDetailsDialog.IsValidSeriesScore(format, participant1Score, participant2Score));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"message\":\"The reported score is not decisive.\"}", "The reported score is not decisive.")]
    [InlineData(HttpStatusCode.BadRequest, "{}", "Try again.")]
    [InlineData(HttpStatusCode.InternalServerError, "{\"message\":\"database password leaked\"}", "Try again.")]
    [InlineData(HttpStatusCode.Unauthorized, "{}", "Sign in to manage this match.")]
    [InlineData(HttpStatusCode.Forbidden, "{}", "You are not authorized to perform this match action.")]
    [InlineData(HttpStatusCode.Conflict, "{\"code\":\"match_reversal_blocked\"}", "This result cannot be reversed because a linked downstream match has already been played or resolved.")]
    public async Task ErrorMessage_UsesOnlyValidationMessagesForBadRequests(
        HttpStatusCode statusCode,
        string content,
        string expected)
    {
        var exception = await CreateMatchApiException(statusCode, content);

        Assert.Equal(expected, TournamentMatchDetailsDialog.GetErrorMessage(exception, "Try again."));
    }

    [Fact]
    public void InitialScores_PreferTheAuthorizedParticipantFullReport()
    {
        var state = CreateActionState(MatchParticipantSide.Participant2);
        state.Participant1ReportedScore1 = 2;
        state.Participant1ReportedScore2 = 0;
        state.Participant2ReportedScore1 = 1;
        state.Participant2ReportedScore2 = 0;

        var scores = TournamentMatchDetailsDialog.GetInitialScores(state);

        Assert.Equal(1, scores.Participant1Score);
        Assert.Equal(0, scores.Participant2Score);
    }

    [Fact]
    public void InitialScores_DoNotCombineMismatchedOrOneSidedReports()
    {
        var mismatchedAdminState = CreateActionState();
        mismatchedAdminState.Participant1ReportedScore1 = 2;
        mismatchedAdminState.Participant1ReportedScore2 = 0;
        mismatchedAdminState.Participant2ReportedScore1 = 1;
        mismatchedAdminState.Participant2ReportedScore2 = 0;

        var mismatchedScores = TournamentMatchDetailsDialog.GetInitialScores(mismatchedAdminState);

        Assert.Null(mismatchedScores.Participant1Score);
        Assert.Null(mismatchedScores.Participant2Score);

        var oneSidedState = CreateActionState(MatchParticipantSide.Participant1);
        oneSidedState.Participant1ReportedScore1 = 2;

        var oneSidedScores = TournamentMatchDetailsDialog.GetInitialScores(oneSidedState);

        Assert.Null(oneSidedScores.Participant1Score);
        Assert.Null(oneSidedScores.Participant2Score);
    }

    [Fact]
    public void AdminReports_AreRenderedOnlyWhenProtectedReportsArePresent()
    {
        var adminState = CreateActionState();
        adminState.Participant1ReportedScore1 = 2;
        adminState.Participant1ReportedScore2 = 0;
        adminState.Participant2ReportedScore1 = 1;
        adminState.Participant2ReportedScore2 = 0;

        Assert.True(TournamentMatchDetailsDialog.ShouldRenderAdminReports(adminState));

        adminState.AuthorizedParticipant = MatchParticipantSide.Participant1;

        Assert.True(TournamentMatchDetailsDialog.ShouldRenderAdminReports(adminState));

        adminState.Participant1ReportedScore1 = null;
        adminState.Participant1ReportedScore2 = null;
        adminState.Participant2ReportedScore1 = null;
        adminState.Participant2ReportedScore2 = null;

        Assert.False(TournamentMatchDetailsDialog.ShouldRenderAdminReports(adminState));
    }

    [Fact]
    public void BlockedReason_UsesTheReasonForTheRequestedAdministratorAction()
    {
        var state = CreateActionState();
        state.ResolveBlockedReason = "match_requires_admin_resolution";
        state.ForceForfeitBlockedReason = "tournament_not_in_progress";
        state.ReverseBlockedReason = "match_reversal_blocked";

        Assert.Equal(
            "This match already requires administrator resolution.",
            TournamentMatchDetailsDialog.GetBlockedReason(
                state,
                TournamentMatchDetailsDialog.MatchMutationAction.Resolve));
        Assert.Equal(
            "This tournament is no longer in progress.",
            TournamentMatchDetailsDialog.GetBlockedReason(
                state,
                TournamentMatchDetailsDialog.MatchMutationAction.ForceForfeit));
        Assert.Equal(
            "This result cannot be reversed because a linked downstream match has already been played or resolved.",
            TournamentMatchDetailsDialog.GetBlockedReason(
                state,
                TournamentMatchDetailsDialog.MatchMutationAction.Reverse));
    }

    [Theory]
    [InlineData(
        "SubmitScore",
        "Score submission is no longer available.")]
    [InlineData(
        "Forfeit",
        "Forfeiting is no longer available for your side.")]
    public void BlockedReason_ForParticipantActions_IsActionSpecific(
        string actionName,
        string expectedMessage)
    {
        var state = CreateActionState();
        state.ResolveBlockedReason = "match_requires_admin_resolution";
        state.ForceForfeitBlockedReason = "tournament_not_in_progress";
        state.ReverseBlockedReason = "match_reversal_blocked";
        var action = Enum.Parse<TournamentMatchDetailsDialog.MatchMutationAction>(actionName);

        Assert.Equal(expectedMessage, TournamentMatchDetailsDialog.GetBlockedReason(state, action));
    }

    [Fact]
    public void ReconcileSelectedMatch_UsesTheRefreshedSameIdInstance()
    {
        var matchId = Guid.NewGuid();
        var selectedMatch = new Match
        {
            Id = matchId,
            LifecycleState = MatchLifecycleState.Disputed
        };
        var refreshedMatch = new Match
        {
            Id = matchId,
            LifecycleState = MatchLifecycleState.Completed,
            ResultVersion = 3
        };
        var tournament = new TournamentExtended { Matches = [refreshedMatch] };

        var reconciled = TournamentDetail.ReconcileSelectedMatch(selectedMatch, tournament);

        Assert.Same(refreshedMatch, reconciled);
        Assert.Equal(MatchLifecycleState.Completed, reconciled!.LifecycleState);
        Assert.Null(TournamentDetail.ReconcileSelectedMatch(selectedMatch, new TournamentExtended()));
    }

    [Fact]
    public void ChildRefresh_IsPreservedWhenParentReloadFails()
    {
        var matchId = Guid.NewGuid();
        var staleSelectedMatch = new Match
        {
            Id = matchId,
            LifecycleState = MatchLifecycleState.Disputed,
            ResultVersion = 2
        };
        var refreshedMatch = new Match
        {
            Id = matchId,
            LifecycleState = MatchLifecycleState.Completed,
            ResultVersion = 3
        };

        var tournament = new TournamentExtended { Matches = [staleSelectedMatch] };

        // The parent applies this projection before its tournament reload. A failed
        // reload must leave both the schedule/bracket row and dialog bound to the child
        // refresh, not the old same-ID object.
        var preserved = TournamentDetail.ApplyRefreshedMatchProjection(
            tournament,
            staleSelectedMatch,
            refreshedMatch);

        Assert.Same(refreshedMatch, preserved);
        Assert.Same(refreshedMatch, tournament.Matches.Single());
        Assert.Equal(MatchLifecycleState.Completed, preserved!.LifecycleState);
        Assert.Equal(3, preserved.ResultVersion);
    }

    [Fact]
    public void PublicFallbackRefresh_IsPropagatedAsTheFreshParentProjection()
    {
        var matchId = Guid.NewGuid();
        var staleSelectedMatch = new Match
        {
            Id = matchId,
            LifecycleState = MatchLifecycleState.ScoreConfirmation,
            ResultVersion = 4,
            Participant1Score = 1,
            Participant2Score = 0
        };
        var publicMatch = new Match
        {
            Id = matchId,
            LifecycleState = MatchLifecycleState.Completed,
            ResultVersion = 5,
            Participant1Score = 2,
            Participant2Score = 1
        };
        var tournament = new TournamentExtended { Matches = [staleSelectedMatch] };

        var selected = TournamentDetail.ApplyRefreshedMatchProjection(
            tournament,
            staleSelectedMatch,
            publicMatch);

        Assert.Same(publicMatch, selected);
        Assert.Same(publicMatch, tournament.Matches.Single());
        Assert.Equal(2, selected!.Participant1Score);
        Assert.Equal(1, selected.Participant2Score);
    }

    [Theory]
    [InlineData(BracketType.SingleElimination)]
    [InlineData(BracketType.DoubleElimination)]
    public void BracketRefreshThenClose_PreservesFreshProjectionWhenParentReloadFails(BracketType bracketType)
    {
        var matchId = Guid.NewGuid();
        var staleMatch = new Match
        {
            Id = matchId,
            BracketType = bracketType,
            LifecycleState = MatchLifecycleState.ScoreConfirmation,
            ResultVersion = 4
        };
        var refreshedMatch = new Match
        {
            Id = matchId,
            BracketType = bracketType,
            LifecycleState = MatchLifecycleState.Completed,
            ResultVersion = 5,
            Participant1Score = 2,
            Participant2Score = 1
        };
        var tournament = new TournamentExtended { BracketType = bracketType, Matches = [staleMatch] };

        // The dialog sends this projection through the bracket wrapper before close.
        // A failed parent reload must not reintroduce the wrapper's stale instance.
        var selected = TournamentDetail.ApplyRefreshedMatchProjection(
            tournament,
            staleMatch,
            refreshedMatch);

        Assert.Same(refreshedMatch, selected);
        Assert.Same(refreshedMatch, tournament.Matches.Single());
        Assert.Equal(MatchLifecycleState.Completed, tournament.Matches.Single().LifecycleState);
    }

    private static MatchActionStateDTO CreateActionState(MatchParticipantSide? authorizedParticipant = null) => new()
    {
        Match = new Match { Id = Guid.NewGuid() },
        AuthorizedParticipant = authorizedParticipant
    };

    private static async Task<ApiException> CreateMatchApiException(HttpStatusCode statusCode, string content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.test/matches/score");
        using var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return await ApiException.Create(request, HttpMethod.Put, response, new RefitSettings(), innerException: null);
    }
}
