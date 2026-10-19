using System.Net;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Refit;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments;

public partial class MatchResolutions : IDisposable
{
    [Inject] private ITournamentService TournamentService { get; set; } = null!;

    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<PendingMatchResolutionDTO> _items = [];
    private bool _isLoading = true;
    private bool _isUnauthorized;
    private string? _loadError;
    private string? _openError;
    private Guid? _openingMatchId;
    private Match? _selectedMatch;
    private TournamentExtended? _selectedTournament;
    private string _selectedParticipant1Name = string.Empty;
    private string _selectedParticipant2Name = string.Empty;
    private bool _disposed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Load once the circuit is interactive so prerendering never calls the API.
        if(firstRender)
            await LoadAsync(showLoading: false);
    }

    private async Task RetryAsync() => await LoadAsync(showLoading: true);

    private async Task LoadAsync(bool showLoading)
    {
        if(showLoading)
        {
            _isLoading = true;
            StateHasChanged();
        }

        try
        {
            _items = PendingMatchResolutionOrdering.ByUrgency(await TournamentService.GetPendingMatchResolutionsAsync(_lifetime.Token));
            _loadError = null;
            _isUnauthorized = false;
        }
        catch(OperationCanceledException) when(_disposed || _lifetime.IsCancellationRequested)
        {
            return;
        }
        catch(ApiException exception) when(exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _isUnauthorized = true;
        }
        catch(UnauthorizedAccessException)
        {
            _isUnauthorized = true;
        }
        catch(Exception)
        {
            _loadError = Localization["Feature.matchResolutions.loadFailed"];
        }
        finally
        {
            _isLoading = false;
        }

        if(!_disposed)
            await InvokeAsync(StateHasChanged);
    }

    private async Task OpenAsync(PendingMatchResolutionDTO item)
    {
        if(_openingMatchId is not null)
            return;

        _openError = null;
        _openingMatchId = item.MatchId;
        try
        {
            var tournament = await TournamentService.GetTournamentByIdAsync(item.TournamentId, _lifetime.Token);
            var match = tournament?.Matches.FirstOrDefault(candidate => candidate.Id == item.MatchId);
            if(tournament is null || match is null)
            {
                // The match left the queue or its tournament changed since the list loaded.
                _openError = Localization["Feature.matchResolutions.openNotFound"];
                await LoadAsync(showLoading: false);
                return;
            }

            _selectedTournament = tournament;
            _selectedMatch = match;
            _selectedParticipant1Name = item.Participant1Name;
            _selectedParticipant2Name = item.Participant2Name;
        }
        catch(OperationCanceledException) when(_disposed || _lifetime.IsCancellationRequested)
        {
            return;
        }
        catch(Exception)
        {
            _openError = Localization["Feature.matchResolutions.openFailed"];
        }
        finally
        {
            _openingMatchId = null;
        }
    }

    private async Task CloseDialogAsync()
    {
        _selectedMatch = null;
        _selectedTournament = null;
        // The admin may have resolved or forfeited the match: show the up to date queue.
        await LoadAsync(showLoading: false);
    }

    private Task HandleMatchRefreshedAsync(Match refreshedMatch)
    {
        if(_selectedMatch?.Id == refreshedMatch.Id)
            _selectedMatch = refreshedMatch;

        return Task.CompletedTask;
    }

    private async Task HandleMatchDataReloadAsync(Match refreshedMatch)
    {
        if(_selectedMatch?.Id == refreshedMatch.Id)
            _selectedMatch = refreshedMatch;

        await LoadAsync(showLoading: false);
    }

    private string GetReasonText(MatchResolutionReason reason) => reason switch
    {
        MatchResolutionReason.Disputed => Localization["Feature.matchResolutions.reasonDisputed"],
        MatchResolutionReason.MissingOpponentReport => Localization["Feature.matchResolutions.reasonMissingReport"],
        _ => Localization["Feature.matchResolutions.reasonUnknown"]
    };

    // A missing or unknown assignment (older backend) shows no label.
    internal string? GetAssignmentLabel(PendingMatchResolutionAssignment? assignment) => assignment switch
    {
        PendingMatchResolutionAssignment.Assigned => Localization["Feature.matchResolutions.assignmentAssigned"],
        PendingMatchResolutionAssignment.Unassigned => Localization["Feature.matchResolutions.assignmentUnassigned"],
        PendingMatchResolutionAssignment.AssignedAdminUnavailable => Localization["Feature.matchResolutions.assignmentUnavailable"],
        PendingMatchResolutionAssignment.Escalated => Localization["Feature.matchResolutions.assignmentEscalated"],
        _ => null
    };

    internal static string GetAssignmentClass(PendingMatchResolutionAssignment? assignment) =>
        assignment is PendingMatchResolutionAssignment.Escalated or PendingMatchResolutionAssignment.AssignedAdminUnavailable
            ? "match-resolution-assignment match-resolution-assignment--urgent"
            : "match-resolution-assignment";

    private string FormatSince(DateTime requiredSinceUtc) =>
        Localization.FormatDateTime(requiredSinceUtc.ToLocalDisplayTime());

    public void Dispose()
    {
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
