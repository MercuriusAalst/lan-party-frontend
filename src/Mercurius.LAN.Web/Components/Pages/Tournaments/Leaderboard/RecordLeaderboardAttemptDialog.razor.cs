using System.Globalization;
using System.Net;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Search;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Refit;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Leaderboard;

public partial class RecordLeaderboardAttemptDialog : IDisposable
{
    private const int UserSearchDebounceMilliseconds = 300;
    private const int MinimumUserSearchLength = 3;
    private static readonly string[] DurationUnitNames = ["Hours", "Minutes", "Seconds", "Milliseconds"];

    private enum EntryMode
    {
        ExistingParticipant,
        LinkedUser,
        Guest
    }

    [Parameter] public Guid TournamentId { get; set; }
    [Parameter] public LeaderboardRankingMetric RankingMetric { get; set; }
    [Parameter] public EventCallback<bool> OnClose { get; set; }

    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IUserSearchService UserSearchService { get; set; } = null!;
    [Inject] private Blazored.Toast.Services.IToastService ToastService { get; set; } = null!;

    private AdminLeaderboardResponseDTO? _adminData;
    private bool _isLoading = true;
    private string? _loadError;
    private string? _submitError;
    private bool _isSubmitting;
    private bool _hasChanges;

    private EntryMode _mode = EntryMode.Guest;
    private Guid? _selectedParticipantId;
    private Guid? _selectedUserId;
    private string? _selectedUserLabel;
    private string _userQuery = string.Empty;
    private IReadOnlyList<GlobalSearchResultDTO> _userResults = [];
    private bool _isSearchingUsers;
    private long _userSearchGeneration;
    private CancellationTokenSource? _userSearchCancellation;
    private string _guestName = string.Empty;
    private string _valueInput = string.Empty;
    private string[] _durationInput = [string.Empty, string.Empty, string.Empty, string.Empty];

    private Guid? _editingAttemptId;
    private string _editValueInput = string.Empty;
    private string[] _editDurationInput = [string.Empty, string.Empty, string.Empty, string.Empty];
    private bool _isSavingAttempt;
    private Guid? _pendingRemovalAttemptId;
    private Guid? _removingAttemptId;

    private IReadOnlyList<AdminLeaderboardParticipantDTO> Participants =>
        _adminData?.Participants ?? [];

    private bool HasParticipants => Participants.Count > 0;

    private AdminLeaderboardParticipantDTO? SelectedParticipant => _mode switch
    {
        EntryMode.ExistingParticipant when _selectedParticipantId.HasValue =>
            Participants.FirstOrDefault(participant => participant.Id == _selectedParticipantId.Value),
        EntryMode.LinkedUser when _selectedUserId.HasValue =>
            Participants.FirstOrDefault(participant => participant.LinkedUserId == _selectedUserId.Value),
        _ => null
    };

    protected override async Task OnInitializedAsync()
    {
        await ReloadAdminAsync();
        if(HasParticipants)
            _mode = EntryMode.ExistingParticipant;
    }

    private async Task ReloadAdminAsync()
    {
        _isLoading = true;
        try
        {
            _adminData = await TournamentService.GetAdminLeaderboardAsync(TournamentId);
            _loadError = null;
        }
        catch(UnauthorizedAccessException)
        {
            _loadError = Localization["Feature.leaderboard.unauthorized"];
        }
        catch(ApiException exception)
        {
            _loadError = ResolveApiError(exception, Localization["Feature.leaderboard.loadFailed"]);
        }
        catch(Exception)
        {
            _loadError = Localization["Feature.leaderboard.loadFailed"];
        }
        finally
        {
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void SetMode(EntryMode mode)
    {
        CancelPendingUserSearch();
        _mode = mode;
        _selectedParticipantId = null;
        _selectedUserId = null;
        _selectedUserLabel = null;
        _userQuery = string.Empty;
        _userResults = [];
        _isSearchingUsers = false;
        _guestName = string.Empty;
        _valueInput = string.Empty;
        _durationInput = [string.Empty, string.Empty, string.Empty, string.Empty];
        _editingAttemptId = null;
        _pendingRemovalAttemptId = null;
        _submitError = null;
    }

    private string ModeOptionClass(EntryMode mode) =>
        _mode == mode ? "leaderboard-mode-option--active" : string.Empty;

    private void HandleParticipantChanged(ChangeEventArgs args)
    {
        _submitError = null;
        _editingAttemptId = null;
        _pendingRemovalAttemptId = null;
        _selectedParticipantId = Guid.TryParse(args.Value?.ToString(), out var participantId)
            ? participantId
            : null;
    }

    internal async Task HandleUserQueryChangedAsync(ChangeEventArgs args)
    {
        _submitError = null;
        _userQuery = args.Value?.ToString() ?? string.Empty;
        _selectedUserId = null;
        _selectedUserLabel = null;

        var query = _userQuery.Trim();
        CancelPendingUserSearch();
        _userResults = [];
        if(query.Length < MinimumUserSearchLength)
        {
            _isSearchingUsers = false;
            return;
        }

        _isSearchingUsers = true;
        var generation = _userSearchGeneration;
        _userSearchCancellation = new CancellationTokenSource();
        var cancellationToken = _userSearchCancellation.Token;
        try
        {
            await Task.Delay(UserSearchDebounceMilliseconds, cancellationToken);

            var results = await UserSearchService.SearchAsync(query, cancellationToken);
            if(cancellationToken.IsCancellationRequested || generation != _userSearchGeneration)
                return;

            _userResults = results
                .Where(result => result.Type == GlobalSearchResultType.User && result.UserId.HasValue)
                .ToList();
        }
        catch(OperationCanceledException)
        {
            // A newer query superseded this search.
        }
        catch(Exception)
        {
            if(!cancellationToken.IsCancellationRequested && generation == _userSearchGeneration)
                _userResults = [];
        }
        finally
        {
            if(!cancellationToken.IsCancellationRequested && generation == _userSearchGeneration)
            {
                _isSearchingUsers = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private void CancelPendingUserSearch()
    {
        _userSearchGeneration++;
        var cancellation = _userSearchCancellation;
        if(cancellation is null)
            return;

        _userSearchCancellation = null;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void SelectUser(GlobalSearchResultDTO result)
    {
        if(!result.UserId.HasValue)
            return;

        _selectedUserId = result.UserId.Value;
        _selectedUserLabel = result.DisplayLabel;
        _userQuery = result.DisplayLabel;
        CancelPendingUserSearch();
        _userResults = [];
        _isSearchingUsers = false;
        _submitError = null;
    }

    private async Task SubmitAttemptAsync()
    {
        if(_isSubmitting)
            return;

        _submitError = null;
        if(!TryBuildRequest(out var request, out var error))
        {
            _submitError = error;
            return;
        }

        _isSubmitting = true;
        try
        {
            var participant = await TournamentService.RecordLeaderboardAttemptAsync(TournamentId, request!);
            _hasChanges = true;
            _valueInput = string.Empty;
            _durationInput = [string.Empty, string.Empty, string.Empty, string.Empty];
            ToastService.ShowSuccess(Localization["Feature.leaderboard.attemptRecorded"]);

            if(participant is not null && participant.Id != Guid.Empty)
            {
                _selectedParticipantId = participant.Id;
                _mode = EntryMode.ExistingParticipant;
                _guestName = string.Empty;
            }
            else if(_mode == EntryMode.Guest)
            {
                _guestName = string.Empty;
            }

            await ReloadAdminAsync();
        }
        catch(LeaderboardConflictException)
        {
            _submitError = Localization["Feature.leaderboard.conflict"];
            await ReloadAdminAsync();
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Conflict)
        {
            _submitError = Localization["Feature.leaderboard.conflict"];
            await ReloadAdminAsync();
        }
        catch(ApiException exception)
        {
            _submitError = ResolveApiError(exception);
        }
        catch(UnauthorizedAccessException)
        {
            _submitError = Localization["Feature.leaderboard.unauthorized"];
        }
        catch(InvalidOperationException exception)
        {
            _submitError = string.IsNullOrWhiteSpace(exception.Message)
                ? Localization["Feature.leaderboard.submitFailed"]
                : exception.Message;
        }
        catch(Exception)
        {
            _submitError = Localization["Feature.leaderboard.submitFailed"];
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private bool TryBuildRequest(out RecordLeaderboardAttemptDTO? request, out string? error)
    {
        request = null;
        error = null;

        switch(_mode)
        {
            case EntryMode.ExistingParticipant when !_selectedParticipantId.HasValue:
                error = Localization["Feature.leaderboard.validationParticipant"];
                return false;
            case EntryMode.LinkedUser when !_selectedUserId.HasValue:
                error = Localization["Feature.leaderboard.validationUser"];
                return false;
            case EntryMode.Guest when string.IsNullOrWhiteSpace(_guestName):
                error = Localization["Feature.leaderboard.validationGuestName"];
                return false;
        }

        decimal? score;
        long? durationMilliseconds;
        if(RankingMetric == LeaderboardRankingMetric.FastestTime)
        {
            score = null;
            if(!TryBuildDurationMilliseconds(_durationInput, out var parsedDuration))
            {
                error = Localization["Feature.leaderboard.validationDuration"];
                return false;
            }

            durationMilliseconds = parsedDuration;
        }
        else if(!TryBuildValue(_valueInput, out score, out durationMilliseconds, out error))
        {
            return false;
        }

        request = new RecordLeaderboardAttemptDTO
        {
            ParticipantId = _mode == EntryMode.ExistingParticipant ? _selectedParticipantId : null,
            LinkedUserId = _mode == EntryMode.LinkedUser ? _selectedUserId : null,
            GuestDisplayName = _mode == EntryMode.Guest ? _guestName.Trim() : null,
            Score = score,
            DurationMilliseconds = durationMilliseconds
        };
        return true;
    }

    internal bool TryBuildValue(
        string rawValue,
        out decimal? score,
        out long? durationMilliseconds,
        out string? error)
    {
        score = null;
        durationMilliseconds = null;
        error = null;

        var raw = rawValue.Trim();
        if(raw.Length == 0)
        {
            error = Localization["Feature.leaderboard.validationValueRequired"];
            return false;
        }

        var normalized = raw.Replace(',', '.');
        if(!decimal.TryParse(
               normalized,
               NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
               CultureInfo.InvariantCulture,
               out var parsedScore) ||
           parsedScore < 0 ||
           decimal.Round(parsedScore, 6) != parsedScore ||
           parsedScore >= 1000000000000m)
        {
            error = Localization["Feature.leaderboard.validationScore"];
            return false;
        }

        score = parsedScore;
        return true;
    }

    internal static bool TryBuildDurationMilliseconds(string[] inputs, out long durationMilliseconds)
    {
        durationMilliseconds = 0;
        if(inputs.Length != 4 ||
           !TryParseAsciiInteger(inputs[0].AsSpan(), out var hours, allowEmpty: true) ||
           !TryParseAsciiInteger(inputs[1].AsSpan(), out var minutes, allowEmpty: true) || minutes > 59 ||
           !TryParseAsciiInteger(inputs[2].AsSpan(), out var seconds, allowEmpty: true) || seconds > 59 ||
           !TryParseAsciiInteger(inputs[3].AsSpan(), out var milliseconds, allowEmpty: true) || milliseconds > 999)
        {
            return false;
        }

        try
        {
            durationMilliseconds = checked(checked(checked(hours * 60 + minutes) * 60 + seconds) * 1000 + milliseconds);
            return durationMilliseconds > 0;
        }
        catch(OverflowException)
        {
            durationMilliseconds = 0;
            return false;
        }
    }

    private static bool TryParseAsciiInteger(ReadOnlySpan<char> value, out long number, bool allowEmpty = false)
    {
        number = 0;
        if(value.IsEmpty)
            return allowEmpty;

        try
        {
            foreach(var character in value)
            {
                if(character is < '0' or > '9')
                    return false;

                number = checked(number * 10 + character - '0');
            }

            return true;
        }
        catch(OverflowException)
        {
            number = 0;
            return false;
        }
    }

    private void BeginAttemptEdit(LeaderboardAttemptDTO attempt)
    {
        _submitError = null;
        _pendingRemovalAttemptId = null;
        _editingAttemptId = attempt.Id;
        if(attempt.Score.HasValue)
        {
            _editValueInput = LeaderboardFormattingExtensions.FormatLeaderboardScore(attempt.Score.Value);
            _editDurationInput = [string.Empty, string.Empty, string.Empty, string.Empty];
        }
        else if(attempt.DurationMilliseconds is { } durationMilliseconds)
        {
            var totalSeconds = durationMilliseconds / 1000;
            _editDurationInput =
            [
                (totalSeconds / 3600).ToString(CultureInfo.InvariantCulture),
                (totalSeconds / 60 % 60).ToString(CultureInfo.InvariantCulture),
                (totalSeconds % 60).ToString(CultureInfo.InvariantCulture),
                (durationMilliseconds % 1000).ToString(CultureInfo.InvariantCulture)
            ];
            _editValueInput = string.Empty;
        }
        else
        {
            _editValueInput = string.Empty;
            _editDurationInput = [string.Empty, string.Empty, string.Empty, string.Empty];
        }
    }

    private void CancelAttemptEdit()
    {
        _editingAttemptId = null;
        _editValueInput = string.Empty;
        _editDurationInput = [string.Empty, string.Empty, string.Empty, string.Empty];
    }

    private async Task SaveAttemptEditAsync(LeaderboardAttemptDTO attempt)
    {
        if(_isSavingAttempt)
            return;

        _submitError = null;
        decimal? score;
        long? durationMilliseconds;
        string? error = null;
        if(RankingMetric == LeaderboardRankingMetric.FastestTime)
        {
            score = null;
            if(!TryBuildDurationMilliseconds(_editDurationInput, out var parsedDuration))
                error = Localization["Feature.leaderboard.validationDuration"];
            durationMilliseconds = parsedDuration;
        }
        else if(!TryBuildValue(_editValueInput, out score, out durationMilliseconds, out error))
        {
            _submitError = error;
            return;
        }
        if(error is not null)
        {
            _submitError = error;
            return;
        }

        _isSavingAttempt = true;
        try
        {
            await TournamentService.UpdateLeaderboardAttemptAsync(
                TournamentId,
                attempt.Id,
                new UpdateLeaderboardAttemptDTO
                {
                    Score = score,
                    DurationMilliseconds = durationMilliseconds,
                    RowVersion = attempt.RowVersion
                });

            _hasChanges = true;
            CancelAttemptEdit();
            ToastService.ShowSuccess(Localization["Feature.leaderboard.attemptUpdated"]);
            await ReloadAdminAsync();
        }
        catch(LeaderboardConflictException)
        {
            _submitError = Localization["Feature.leaderboard.conflict"];
            await ReloadAdminAsync();
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Conflict)
        {
            _submitError = Localization["Feature.leaderboard.conflict"];
            await ReloadAdminAsync();
        }
        catch(ApiException exception)
        {
            _submitError = ResolveApiError(exception);
        }
        catch(UnauthorizedAccessException)
        {
            _submitError = Localization["Feature.leaderboard.unauthorized"];
        }
        catch(InvalidOperationException exception)
        {
            _submitError = string.IsNullOrWhiteSpace(exception.Message)
                ? Localization["Feature.leaderboard.submitFailed"]
                : exception.Message;
        }
        catch(Exception)
        {
            _submitError = Localization["Feature.leaderboard.submitFailed"];
        }
        finally
        {
            _isSavingAttempt = false;
        }
    }

    private void BeginAttemptRemoval(LeaderboardAttemptDTO attempt)
    {
        _submitError = null;
        _editingAttemptId = null;
        _pendingRemovalAttemptId = attempt.Id;
    }

    private void CancelAttemptRemoval()
    {
        _pendingRemovalAttemptId = null;
    }

    private async Task RemoveAttemptAsync(LeaderboardAttemptDTO attempt)
    {
        if(_removingAttemptId.HasValue)
            return;

        _submitError = null;
        _removingAttemptId = attempt.Id;
        try
        {
            await TournamentService.DeleteLeaderboardAttemptAsync(TournamentId, attempt.Id, attempt.RowVersion);
            _hasChanges = true;
            _pendingRemovalAttemptId = null;
            ToastService.ShowSuccess(Localization["Feature.leaderboard.attemptRemoved"]);
            await ReloadAdminAsync();
        }
        catch(LeaderboardConflictException)
        {
            _submitError = Localization["Feature.leaderboard.conflict"];
            await ReloadAdminAsync();
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Conflict)
        {
            _submitError = Localization["Feature.leaderboard.conflict"];
            await ReloadAdminAsync();
        }
        catch(ApiException exception)
        {
            _submitError = ResolveApiError(exception);
        }
        catch(UnauthorizedAccessException)
        {
            _submitError = Localization["Feature.leaderboard.unauthorized"];
        }
        catch(InvalidOperationException exception)
        {
            _submitError = string.IsNullOrWhiteSpace(exception.Message)
                ? Localization["Feature.leaderboard.submitFailed"]
                : exception.Message;
        }
        catch(Exception)
        {
            _submitError = Localization["Feature.leaderboard.submitFailed"];
        }
        finally
        {
            _removingAttemptId = null;
        }
    }

    private string ResolveApiError(ApiException exception, string? fallback = null)
    {
        if(exception.StatusCode == HttpStatusCode.Forbidden)
            return Localization["Feature.leaderboard.unauthorized"];
        if(exception.StatusCode == HttpStatusCode.NotFound)
            return Localization["Feature.leaderboard.notFound"];

        var message = exception.GetApiError()?.Message;
        return string.IsNullOrWhiteSpace(message)
            ? fallback ?? Localization["Feature.leaderboard.submitFailed"]
            : message;
    }

    private string FormatAttemptValue(LeaderboardAttemptDTO attempt) =>
        LeaderboardFormattingExtensions.FormatLeaderboardValue(
            RankingMetric,
            attempt.Score,
            attempt.DurationMilliseconds);

    private string GetParticipantLabel(AdminLeaderboardParticipantDTO participant) =>
        participant.ParticipantKind == LeaderboardParticipantKind.Guest
            ? Localization.Get("Feature.leaderboard.guestParticipantLabel", participant.DisplayName)
            : participant.DisplayName;

    private string GetValueLabel() => Localization["Feature.leaderboard.scoreLabel"];

    private Task RequestClose()
    {
        if(OnClose.HasDelegate)
            return OnClose.InvokeAsync(_hasChanges);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        CancelPendingUserSearch();
    }
}
