using System.Net;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Refit;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Matches.DetailView;

public partial class TournamentMatchDetailsDialog : IAsyncDisposable
{
    internal enum MatchMutationAction
    {
        ConfirmEnded,
        SubmitScore,
        Forfeit,
        ForceForfeit,
        Resolve,
        Reverse
    }

    [Parameter] public Match Match { get; set; } = null!;
    [Parameter] public TournamentExtended Tournament { get; set; } = null!;
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<Match> OnDataReload { get; set; }
    [Parameter] public EventCallback<Match> OnMatchRefreshed { get; set; }
    [Parameter] public string Participant2Name { get; set; } = null!;
    [Parameter] public string Participant1Name { get; set; } = null!;
    [Parameter] public ParticipantViewModel? Participant1 { get; set; }
    [Parameter] public ParticipantViewModel? Participant2 { get; set; }

    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private Guid? Participant1Id => Match.ParticipationMode == ParticipationMode.Team ? Match.TeamParticipant1Id : Match.UserParticipant1Id;
    private Guid? Participant2Id => Match.ParticipationMode == ParticipationMode.Team ? Match.TeamParticipant2Id : Match.UserParticipant2Id;
    private Guid? WinnerId => Match.ParticipationMode == ParticipationMode.Team ? Match.TeamWinnerId : Match.UserWinnerId;
    private ParticipantViewModel? HeaderParticipant1 => Participant1 ?? GetParticipantById(Participant1Id);
    private ParticipantViewModel? HeaderParticipant2 => Participant2 ?? GetParticipantById(Participant2Id);
    private TournamentParticipantLookup _participantLookup = TournamentParticipantLookup.Empty;
    private MatchActionStateDTO? _actionState;
    private Guid _loadedMatchId;
    private int _refreshGeneration;
    private bool _hasLoaded;
    private bool _isLoading = true;
    private bool _isSubmitting;
    private bool _requiresAuthentication;
    private bool _hasFreshActionState;
    private bool _forfeitConfirmationRequested;
    private MatchParticipantSide? _adminForfeitConfirmationSide;
    private bool _reverseConfirmationRequested;
    private string? _errorMessage;
    private int? _participant1Score;
    private int? _participant2Score;
    private bool HasScoreInputs => _participant1Score.HasValue && _participant2Score.HasValue;
    private CancellationTokenSource? _deadlineRefreshCancellation;
    private DateTime? _deadlineRefreshTriggeredFor;
    private Task? _deadlineRefreshTask;
    private Match? _freshMatchProjection;
    private PublicUserDTO? _selectedUser;
    private CancellationTokenSource? _opponentProfileCancellation;
    private bool _isLoadingOpponentProfile;
    private string? _opponentProfileError;
    private bool _disposed;
    private bool CanViewParticipant1Details => _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant2;
    private bool CanViewParticipant2Details => _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant1;

    protected override async Task OnParametersSetAsync()
    {
        _participantLookup = TournamentParticipantLookup.FromTournament(Tournament);
        if(_loadedMatchId == Match.Id)
        {
            if(_freshMatchProjection is { } freshMatch &&
               freshMatch.Id == Match.Id &&
               Match.ResultVersion < freshMatch.ResultVersion)
                Match = freshMatch;
            else if(_freshMatchProjection is null || Match.ResultVersion >= _freshMatchProjection.ResultVersion)
                _freshMatchProjection = Match;

            return;
        }

        _loadedMatchId = Match.Id;
        CancelOpponentProfileLookup();
        _freshMatchProjection = null;
        _hasLoaded = false;
        _actionState = null;
        _errorMessage = null;
        _requiresAuthentication = false;
        _hasFreshActionState = false;
        _forfeitConfirmationRequested = false;
        _adminForfeitConfirmationSide = null;
        _reverseConfirmationRequested = false;
        StopDeadlineRefresh();
        await RefreshAsync(Match.Id);
    }

    private ParticipantViewModel? GetParticipantById(Guid? participantId) =>
        _participantLookup.Resolve(Match.ParticipationMode, participantId);

    private async Task DisplayUserPopup(PublicUserDTO user)
    {
        CancelOpponentProfileLookup();
        using var cancellation = new CancellationTokenSource();
        _opponentProfileCancellation = cancellation;
        _selectedUser = null;
        _opponentProfileError = null;
        _isLoadingOpponentProfile = true;
        var matchId = Match.Id;

        try
        {
            var profile = await TournamentService.GetMatchOpponentProfileAsync(matchId, cancellation.Token);
            if(_disposed || cancellation.IsCancellationRequested || Match.Id != matchId)
                return;

            _selectedUser = new PublicUserDTO
            {
                Username = profile.Username,
                Firstname = profile.Firstname,
                Lastname = profile.Lastname,
                DiscordId = profile.DiscordId,
                SteamId = profile.SteamId,
                RiotId = profile.RiotId
            };
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested)
        {
        }
        catch(Exception)
        {
            if(!_disposed && !cancellation.IsCancellationRequested && Match.Id == matchId)
                _opponentProfileError = Localization["Feature.match.unavailable"];
        }
        finally
        {
            if(ReferenceEquals(_opponentProfileCancellation, cancellation))
            {
                _opponentProfileCancellation = null;
                _isLoadingOpponentProfile = false;
            }
        }
    }

    private void HideUserInfoPopup()
    {
        _selectedUser = null;
    }

    private void CancelOpponentProfileLookup()
    {
        _opponentProfileCancellation?.Cancel();
        _opponentProfileCancellation?.Dispose();
        _opponentProfileCancellation = null;
        _isLoadingOpponentProfile = false;
    }

    private bool IsWinner(Guid? participantId) => WinnerId != null && participantId == WinnerId;

    private string GetStageLabel() => Match.IsLowerBracketMatch
        ? Localization["Feature.match.lowerBracket"]
        : Localization["Feature.match.mainBracket"];

    private string GetRoundLabel() => Localization.Get("Feature.match.round", Match.RoundNumber);

    private string GetFormatLabel() => Match.Format switch
    {
        TournamentFormat.BestOf1 => Localization["Feature.tournament.formatBestOf1"],
        TournamentFormat.BestOf3 => Localization["Feature.tournament.formatBestOf3"],
        TournamentFormat.BestOf5 => Localization["Feature.tournament.formatBestOf5"],
        _ => Match.Format.ToString()
    };

    private string GetStatusLabel() => Match.LifecycleState switch
    {
        MatchLifecycleState.AwaitingEndedConfirmation => Localization["Feature.match.statusAwaitingConfirmation"],
        MatchLifecycleState.AwaitingScore => Localization["Feature.match.statusReadyForScore"],
        MatchLifecycleState.ScoreConfirmation => Localization["Feature.match.statusScoreConfirmation"],
        MatchLifecycleState.Disputed => Localization["Feature.match.statusDisputed"],
        MatchLifecycleState.AdminResolutionRequired => Localization["Feature.match.statusAdminResolution"],
        MatchLifecycleState.Completed => Localization["Feature.match.statusCompleted"],
        MatchLifecycleState.Forfeited => Localization["Feature.match.statusForfeited"],
        MatchLifecycleState.Reversed => Localization["Feature.match.statusReversed"],
        _ => Localization["Feature.match.statusAwaitingResult"]
    };

    private string GetStatusDescription() => Match.LifecycleState switch
    {
        MatchLifecycleState.AwaitingEndedConfirmation => Localization["Feature.match.descriptionAwaitingConfirmation"],
        MatchLifecycleState.AwaitingScore => Localization["Feature.match.descriptionAwaitingScore"],
        MatchLifecycleState.ScoreConfirmation => Localization["Feature.match.descriptionScoreConfirmation"],
        MatchLifecycleState.Disputed => Localization["Feature.match.descriptionDisputed"],
        MatchLifecycleState.AdminResolutionRequired => Localization["Feature.match.descriptionAdminResolution"],
        MatchLifecycleState.Completed => Localization["Feature.match.descriptionCompleted"],
        MatchLifecycleState.Forfeited => Localization["Feature.match.descriptionForfeited"],
        MatchLifecycleState.Reversed => Localization["Feature.match.descriptionReversed"],
        _ => Localization["Feature.match.descriptionLoading"]
    };

    private string GetStatusClass() => Match.LifecycleState switch
    {
        MatchLifecycleState.Completed or MatchLifecycleState.Forfeited => "match-status-pill--complete",
        MatchLifecycleState.Disputed or MatchLifecycleState.AdminResolutionRequired => "match-status-pill--attention",
        MatchLifecycleState.Reversed => "match-status-pill--reversed",
        _ => "match-status-pill--scheduled"
    };

    private string GetStartDateTimeLabel() =>
        Match.EstimatedStartTime.HasValue
            ? Localization.FormatDateTime(Match.EstimatedStartTime.Value.ToLocalDisplayTime())
            : Localization["Feature.match.unavailable"];

    private string GetDeadlineLabel()
    {
        var deadline = GetDeadlineUtc();
        return deadline.HasValue
            ? Localization.Get("Feature.match.windowCloses", Localization.FormatDateTime(deadline.Value.ToLocalDisplayTime()))
            : string.Empty;
    }

    private string GetDeadlineRemainingLabel()
    {
        var deadline = GetDeadlineUtc();
        if(!deadline.HasValue)
            return string.Empty;

        var remaining = deadline.Value.ToUniversalTime() - DateTime.UtcNow;
        if(remaining <= TimeSpan.Zero)
            return Localization["Feature.match.windowClosed"];

        var minutes = (int)remaining.TotalMinutes;
        var seconds = remaining.Seconds;
        return minutes > 0
            ? Localization.Get("Feature.match.remainingMinutes", minutes, seconds)
            : Localization.Get("Feature.match.remainingSeconds", seconds);
    }

    private DateTime? GetDeadlineUtc() => Match.LifecycleState == MatchLifecycleState.Disputed
        ? Match.CorrectionDeadlineUtc
        : Match.ScoreConfirmationDeadlineUtc;

    private string GetCardClass(Guid? participantId)
    {
        if(IsWinner(participantId))
            return "participant-card winner-card";
        if(WinnerId != null && participantId != null)
            return "participant-card loser-card";
        return "participant-card";
    }

    private static string BuildTeamProfileHref(string teamName) =>
        string.IsNullOrWhiteSpace(teamName)
            ? string.Empty
            : $"/teams/{Uri.EscapeDataString(teamName.Trim())}";

    private bool IsAdminResolutionState() =>
        Match.LifecycleState is MatchLifecycleState.Disputed or MatchLifecycleState.AdminResolutionRequired;

    private bool ShowPrivateReports => _hasFreshActionState && _actionState is not null;

    private int? OwnReport1 => _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant1
        ? _actionState.Participant1ReportedScore1
        : _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant2
            ? _actionState.Participant2ReportedScore1
            : null;

    private int? OwnReport2 => _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant1
        ? _actionState.Participant1ReportedScore2
        : _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant2
            ? _actionState.Participant2ReportedScore2
            : null;

    private int? OpponentReport1 => _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant1
        ? _actionState.Participant2ReportedScore1
        : _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant2
            ? _actionState.Participant1ReportedScore1
            : null;

    private int? OpponentReport2 => _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant1
        ? _actionState.Participant2ReportedScore2
        : _actionState?.AuthorizedParticipant == MatchParticipantSide.Participant2
            ? _actionState.Participant1ReportedScore2
            : null;

    private int? PrivateReport1 => _actionState?.Participant1ReportedScore1 ?? _actionState?.Participant2ReportedScore1;

    private int? PrivateReport2 => _actionState?.Participant1ReportedScore2 ?? _actionState?.Participant2ReportedScore2;

    internal static (int? Participant1Score, int? Participant2Score) GetInitialScores(MatchActionStateDTO state)
    {
        var actorReport = state.AuthorizedParticipant switch
        {
            MatchParticipantSide.Participant1 => GetCompleteReport(
                state.Participant1ReportedScore1,
                state.Participant1ReportedScore2),
            MatchParticipantSide.Participant2 => GetCompleteReport(
                state.Participant2ReportedScore1,
                state.Participant2ReportedScore2),
            _ => null
        };
        if(actorReport is { } report)
            return report;

        var officialResult = GetCompleteReport(
            state.Match.Participant1Score,
            state.Match.Participant2Score);
        return officialResult is { } result
            ? (result.Item1, result.Item2)
            : ((int?)null, (int?)null);
    }

    internal static bool ShouldRenderAdminReports(MatchActionStateDTO state) =>
        (state.Participant1ReportedScore1.HasValue ||
         state.Participant1ReportedScore2.HasValue ||
         state.Participant2ReportedScore1.HasValue ||
         state.Participant2ReportedScore2.HasValue);

    private static (int, int)? GetCompleteReport(int? participant1Score, int? participant2Score) =>
        participant1Score is { } score1 && participant2Score is { } score2
            ? (score1, score2)
            : null;

    private string ActionSubjectLabel => Match.ParticipationMode == ParticipationMode.Team
        ? Localization["Feature.match.yourCaptainActions"]
        : Localization["Feature.match.yourPlayerActions"];

    private string SignInHref => $"/account/login?returnUrl={Uri.EscapeDataString(GetCurrentRelativeUrl())}";

    private string GetCurrentRelativeUrl()
    {
        var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        return string.IsNullOrWhiteSpace(relativePath)
            ? "/"
            : $"/{relativePath}";
    }

    private string GetParticipantName(MatchParticipantSide side) =>
        side == MatchParticipantSide.Participant1 ? Participant1Name : Participant2Name;

    private static string GetAdminBlockedReason(string? reason) => reason switch
    {
        "tournament_not_in_progress" => "This tournament is no longer in progress.",
        "match_not_completed" => "This match does not have an official result to reverse.",
        "match_already_completed" => "This match already has an official result.",
        "match_not_ready" => "Both match sides must be assigned before an administrator can record a forfeit.",
        "match_not_forfeitable" => "This match cannot be forfeited in its current state.",
        "match_not_disputed" => "This match does not currently require administrator resolution.",
        "match_requires_admin_resolution" => "This match already requires administrator resolution.",
        "match_reversal_blocked" => "This result cannot be reversed because a linked downstream match has already been played or resolved.",
        "downstream_graph_too_large" => "This result cannot be reversed until the linked bracket is reviewed by an administrator.",
        "admin_required" => "An authorized tournament administrator is required for this action.",
        _ => "This administrator action is unavailable."
    };

    private string GetLocalizedAdminBlockedReason(string? reason) => reason switch
    {
        "tournament_not_in_progress" => Localization["Feature.match.blockedTournamentNotInProgress"],
        "match_not_completed" => Localization["Feature.match.blockedMatchNotCompleted"],
        "match_already_completed" => Localization["Feature.match.blockedMatchAlreadyCompleted"],
        "match_not_ready" => Localization["Feature.match.blockedMatchNotReady"],
        "match_not_forfeitable" => Localization["Feature.match.blockedMatchNotForfeitable"],
        "match_not_disputed" => Localization["Feature.match.blockedMatchNotDisputed"],
        "match_requires_admin_resolution" => Localization["Feature.match.blockedAdminResolution"],
        "match_reversal_blocked" => Localization["Feature.match.blockedReversal"],
        "downstream_graph_too_large" => Localization["Feature.match.blockedDownstreamGraph"],
        "admin_required" => Localization["Feature.match.blockedAdminRequired"],
        _ => Localization["Feature.match.blockedUnavailable"]
    };

    private string FormatReport(int? participant1Score, int? participant2Score) =>
        participant1Score.HasValue && participant2Score.HasValue
            ? $"{participant1Score}-{participant2Score}"
            : Localization["Feature.match.notSubmitted"];

    private string GetEmptyParticipantLabel(bool isBye) =>
        isBye ? Localization["Feature.match.bye"] : Localization["Feature.match.tbd"];

    private string GetEndedStatusLabel(bool ended) =>
        ended ? Localization["Feature.match.confirmed"] : Localization["Feature.match.waiting"];

    private async Task<bool> RefreshAsync(Guid expectedMatchId)
    {
        var generation = ++_refreshGeneration;
        _isLoading = true;
        _hasFreshActionState = false;
        _errorMessage = null;
        _requiresAuthentication = false;
        await InvokeAsync(StateHasChanged);

        try
        {
            var state = await TournamentService.GetMatchActionStateAsync(expectedMatchId);
            if(generation != _refreshGeneration || Match.Id != expectedMatchId)
                return false;

            Match = state.Match;
            _freshMatchProjection = Match;
            _actionState = state;
            _hasFreshActionState = true;
            (_participant1Score, _participant2Score) = GetInitialScores(state);
            _hasLoaded = true;
            StartDeadlineRefresh();
            await NotifyMatchRefreshedAsync();
            return true;
        }
        catch(ApiException exception) when(exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if(generation != _refreshGeneration || Match.Id != expectedMatchId)
                return false;

            try
            {
                var publicMatch = await TournamentService.GetMatchByIdAsync(expectedMatchId);
                if(generation != _refreshGeneration || Match.Id != expectedMatchId)
                    return false;

                Match = publicMatch;
                _freshMatchProjection = Match;
                _actionState = null;
                _hasFreshActionState = false;
                _participant1Score = publicMatch.Participant1Score;
                _participant2Score = publicMatch.Participant2Score;
                _requiresAuthentication = exception.StatusCode == HttpStatusCode.Unauthorized;
                _hasLoaded = true;
                StartDeadlineRefresh();
                await NotifyMatchRefreshedAsync();
                return true;
            }
            catch(Exception fallbackException)
            {
                if(generation == _refreshGeneration && Match.Id == expectedMatchId)
                {
                    _errorMessage = GetLocalizedErrorMessage(
                        fallbackException,
                        "Feature.match.detailsUnavailable");
                    _hasFreshActionState = false;
                    _hasLoaded = true;
                    StopDeadlineRefresh();
                }

                return false;
            }
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
        {
            if(generation == _refreshGeneration && Match.Id == expectedMatchId)
            {
                _errorMessage = Localization["Feature.match.noLongerAvailable"];
                _hasFreshActionState = false;
                _hasLoaded = true;
            }
            return false;
        }
        catch(Exception exception)
        {
            if(generation == _refreshGeneration && Match.Id == expectedMatchId)
            {
                _errorMessage = GetLocalizedErrorMessage(exception, "Feature.match.detailsUnavailable");
                _hasFreshActionState = false;
                _hasLoaded = true;
            }
            return false;
        }
        finally
        {
            if(generation == _refreshGeneration)
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task NotifyMatchRefreshedAsync()
    {
        if(!OnMatchRefreshed.HasDelegate)
            return;

        try
        {
            await OnMatchRefreshed.InvokeAsync(Match);
        }
        catch(Exception exception)
        {
            ToastService.ShowWarning(GetLocalizedErrorMessage(
                exception,
                "Feature.match.bracketUpdateUnavailable"));
        }
    }

    private void StartDeadlineRefresh()
    {
        StopDeadlineRefresh(resetTrigger: false);
        if(!_hasLoaded || !GetDeadlineUtc().HasValue)
            return;

        _deadlineRefreshCancellation = new CancellationTokenSource();
        _deadlineRefreshTask = RefreshDeadlineAsync(_deadlineRefreshCancellation.Token);
    }

    private void StopDeadlineRefresh(bool resetTrigger = true)
    {
        _deadlineRefreshCancellation?.Cancel();
        _deadlineRefreshCancellation?.Dispose();
        _deadlineRefreshCancellation = null;
        _deadlineRefreshTask = null;
        if(resetTrigger)
            _deadlineRefreshTriggeredFor = null;
    }

    private async Task RefreshDeadlineAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while(await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    if(cancellationToken.IsCancellationRequested)
                        return;

                    var deadline = GetDeadlineUtc();
                    if(!deadline.HasValue)
                    {
                        StopDeadlineRefresh();
                        StateHasChanged();
                        return;
                    }

                    if(deadline.Value.ToUniversalTime() <= DateTime.UtcNow &&
                       !_isLoading &&
                       !_isSubmitting &&
                       _deadlineRefreshTriggeredFor != deadline)
                    {
                        _deadlineRefreshTriggeredFor = deadline;
                        await RefreshAsync(Match.Id);
                        return;
                    }

                    StateHasChanged();
                });
            }
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
        }
        catch(ObjectDisposedException) when(cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RetryAsync() => await RefreshAsync(Match.Id);

    private async Task ConfirmEndedAsync()
    {
        var matchId = Match.Id;
        await RunMutationAsync(
            () => TournamentService.ConfirmMatchEndedAsync(matchId),
            Localization["Feature.match.confirmEndedSaved"],
            state => state.CanConfirmEnded,
            MatchMutationAction.ConfirmEnded);
    }

    private async Task SubmitScoreAsync()
    {
        if(_actionState?.CanSubmitScore != true ||
           _participant1Score is not { } participant1Score ||
           _participant2Score is not { } participant2Score)
            return;

        var matchId = Match.Id;
        await RunMutationAsync(
            () => TournamentService.SubmitMatchScoreAsync(
                matchId,
                new SubmitMatchScoreDTO
                {
                    Participant1Score = participant1Score,
                    Participant2Score = participant2Score
                }),
            Localization["Feature.match.scoreReportSaved"],
            state => state.CanSubmitScore,
            MatchMutationAction.SubmitScore);
    }

    private async Task ForfeitOwnSideAsync()
    {
        if(_actionState?.AuthorizedParticipant is not MatchParticipantSide side)
            return;

        if(!_hasFreshActionState || _actionState?.CanForfeit != true ||
           _forfeitConfirmationRequested == false)
            return;

        var matchId = Match.Id;
        await RunMutationAsync(
            () => TournamentService.ForfeitMatchAsync(matchId, new ForfeitMatchDTO { Participant = side }),
            Localization["Feature.match.forfeitSaved"],
            state => state.CanForfeit,
            MatchMutationAction.Forfeit);
        _forfeitConfirmationRequested = false;
    }

    private Task RequestAdminForfeitAsync(MatchParticipantSide side)
    {
        if(!_hasFreshActionState || _actionState?.CanForceForfeit != true || _isSubmitting || _isLoading)
            return Task.CompletedTask;

        _adminForfeitConfirmationSide = side;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ForfeitSideAsAdminAsync(MatchParticipantSide side)
    {
        if(_adminForfeitConfirmationSide != side || _actionState?.CanForceForfeit != true)
            return;

        var matchId = Match.Id;
        await RunMutationAsync(
            () => TournamentService.ForfeitMatchAsync(matchId, new ForfeitMatchDTO { Participant = side }),
            Localization["Feature.match.adminForfeitSaved"],
            state => state.CanForceForfeit,
            MatchMutationAction.ForceForfeit);
        _adminForfeitConfirmationSide = null;
    }

    private async Task ResolveAsync()
    {
        if(_actionState?.CanResolve != true ||
           _participant1Score is not { } participant1Score ||
           _participant2Score is not { } participant2Score)
            return;

        var matchId = Match.Id;
        await RunMutationAsync(
            () => TournamentService.ResolveMatchAsync(
                matchId,
                new ResolveMatchDTO
                {
                    Participant1Score = participant1Score,
                    Participant2Score = participant2Score
                }),
            Localization["Feature.match.resultResolved"],
            state => state.CanResolve,
            MatchMutationAction.Resolve);
    }

    private async Task ReverseAsync()
    {
        if(_actionState?.CanReverse != true)
            return;

        var matchId = Match.Id;
        await RunMutationAsync(
            () => TournamentService.ReverseMatchAsync(matchId),
            Localization["Feature.match.resultReversed"],
            state => state.CanReverse,
            MatchMutationAction.Reverse);
    }

    private async Task RunMutationAsync(
        Func<Task<Match>> mutation,
        string successMessage,
        Func<MatchActionStateDTO, bool> capability,
        MatchMutationAction action)
    {
        if(_isSubmitting || _isLoading || !_hasLoaded || !_hasFreshActionState || _actionState is null ||
           !capability(_actionState))
            return;

        var expectedMatchId = Match.Id;
        var expectedRefreshGeneration = _refreshGeneration;
        _isSubmitting = true;
        _isLoading = true;
        _errorMessage = null;
        _forfeitConfirmationRequested = false;
        _adminForfeitConfirmationSide = null;
        _reverseConfirmationRequested = false;
        await InvokeAsync(StateHasChanged);

        try
        {
            var latestState = await TournamentService.GetMatchActionStateAsync(expectedMatchId);
            if(expectedMatchId != Match.Id || expectedRefreshGeneration != _refreshGeneration)
                return;

            Match = latestState.Match;
            _freshMatchProjection = Match;
            _actionState = latestState;
            _hasFreshActionState = true;
            await NotifyMatchRefreshedAsync();
            if(!capability(latestState))
            {
                _errorMessage = GetLocalizedBlockedReason(latestState, action);
                return;
            }

            var result = await mutation();
            if(expectedMatchId != Match.Id || expectedRefreshGeneration != _refreshGeneration)
                return;

            Match = result;
            _freshMatchProjection = Match;
            await NotifyMatchRefreshedAsync();
            var refreshed = await RefreshAsync(expectedMatchId);
            if(refreshed)
            {
                if(_requiresAuthentication)
                    ToastService.ShowWarning(Localization["Feature.match.savedSignInToManage"]);
                else
                    ToastService.ShowSuccess(successMessage);

                try
                {
                    await OnDataReload.InvokeAsync(Match);
                }
                catch(Exception exception)
                {
                    // The command and protected match refresh succeeded. Keep that fresh
                    // projection visible even when the surrounding tournament reload fails.
                    _errorMessage = GetLocalizedErrorMessage(
                        exception,
                        "Feature.match.savedTournamentUnavailable");
                    ToastService.ShowWarning(_errorMessage);
                }
            }
            else
            {
                ToastService.ShowWarning(Localization["Feature.match.savedDetailsUnavailable"]);
            }
        }
        catch(Exception exception)
        {
            if(expectedMatchId == Match.Id)
            {
                _hasFreshActionState = false;
                _errorMessage = GetLocalizedErrorMessage(exception, "Feature.match.actionSaveFailed");
                ToastService.ShowError(_errorMessage);
            }
        }
        finally
        {
            if(expectedMatchId == Match.Id)
                _isSubmitting = false;
            if(expectedMatchId == Match.Id)
                _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    internal static string GetBlockedReason(MatchActionStateDTO state, MatchMutationAction action)
    {
        return action switch
        {
            MatchMutationAction.ForceForfeit => GetAdminBlockedReason(state.ForceForfeitBlockedReason),
            MatchMutationAction.Resolve => GetAdminBlockedReason(state.ResolveBlockedReason),
            MatchMutationAction.Reverse => GetAdminBlockedReason(state.ReverseBlockedReason),
            MatchMutationAction.ConfirmEnded => "Match-end confirmation is no longer available.",
            MatchMutationAction.SubmitScore => "Score submission is no longer available.",
            MatchMutationAction.Forfeit => "Forfeiting is no longer available for your side.",
            _ => "The match changed while you were working. Try again."
        };
    }

    private string GetLocalizedBlockedReason(MatchActionStateDTO state, MatchMutationAction action) => action switch
    {
        MatchMutationAction.ForceForfeit => GetLocalizedAdminBlockedReason(state.ForceForfeitBlockedReason),
        MatchMutationAction.Resolve => GetLocalizedAdminBlockedReason(state.ResolveBlockedReason),
        MatchMutationAction.Reverse => GetLocalizedAdminBlockedReason(state.ReverseBlockedReason),
        MatchMutationAction.ConfirmEnded => Localization["Feature.match.confirmEndedUnavailable"],
        MatchMutationAction.SubmitScore => Localization["Feature.match.scoreSubmissionUnavailable"],
        MatchMutationAction.Forfeit => Localization["Feature.match.forfeitUnavailable"],
        _ => Localization["Feature.match.changedTryAgain"]
    };

    private string GetLocalizedErrorMessage(Exception exception, string fallbackKey)
    {
        if(exception is ApiException apiException)
        {
            if(apiException.StatusCode == HttpStatusCode.Unauthorized)
                return Localization["Feature.match.signInToManageShort"];

            if(apiException.StatusCode == HttpStatusCode.Forbidden)
                return Localization["Feature.match.notAuthorized"];

            if(apiException.StatusCode == HttpStatusCode.Conflict)
            {
                var apiError = apiException.GetApiError();
                return apiError?.Code switch
                {
                    "match_reversal_blocked" => Localization["Feature.match.blockedReversal"],
                    "downstream_graph_too_large" => Localization["Feature.match.blockedDownstreamGraph"],
                    "match_requires_admin_resolution" => Localization["Feature.match.blockedAdminResolution"],
                    _ => Localization["Feature.match.changedTryAgain"]
                };
            }
        }

        return Localization[fallbackKey];
    }

    private static string GetErrorMessage(Exception exception, string fallback)
    {
        if(exception is ApiException apiException)
        {
            if(apiException.StatusCode == HttpStatusCode.Unauthorized)
                return "Sign in to manage this match.";
            if(apiException.StatusCode == HttpStatusCode.Forbidden)
            {
                return "You are not authorized to perform this match action.";
            }
            if(apiException.StatusCode == HttpStatusCode.Conflict)
            {
                var apiError = apiException.GetApiError();
                if(apiError?.Code == "match_reversal_blocked")
                    return "This result cannot be reversed because a linked downstream match has already been played or resolved.";
                if(apiError?.Code == "downstream_graph_too_large")
                    return "This result cannot be reversed until the linked bracket is reviewed by an administrator.";
                if(apiError?.Code == "match_requires_admin_resolution")
                    return "This match already requires administrator resolution.";
                return "The match changed while you were working. Try again.";
            }
        }

        return fallback;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        CancelOpponentProfileLookup();
        var refreshTask = _deadlineRefreshTask;
        StopDeadlineRefresh();

        if(refreshTask is null)
            return;

        try
        {
            await refreshTask;
        }
        catch(OperationCanceledException)
        {
        }
        catch(ObjectDisposedException)
        {
        }
    }
}
