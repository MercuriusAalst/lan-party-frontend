using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Refit;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Leaderboard;

public partial class TournamentLeaderboardTab : IDisposable
{
    internal const string ResultEntryUnavailableDescriptionId = "leaderboard-entry-unavailable";

    [Parameter] public TournamentExtended Tournament { get; set; } = null!;
    [Parameter] public EventCallback OnLeaderboardChanged { get; set; }

    [Inject] private ITournamentService TournamentService { get; set; } = null!;

    private PublicLeaderboardDTO? _leaderboard;
    private bool _isLoading = true;
    private string? _loadError;
    private bool _isEntryOpen;
    private Guid? _loadedTournamentId;
    private long _loadGeneration;
    private CancellationTokenSource? _loadCancellation;
    private bool _isDisposed;

    private IReadOnlyList<LeaderboardRowDTO> Rows => _leaderboard?.Rows ?? [];

    private LeaderboardRankingMetric Metric =>
        _leaderboard?.RankingMetric ?? Tournament.ResolveLeaderboardMetric();

    private bool CanManageResults =>
        Tournament.IsLeaderboard() && Tournament.Status == TournamentStatus.InProgress;

    internal string? ResultEntryUnavailableReason =>
        CanManageResults
            ? null
            : Tournament.Status == TournamentStatus.Scheduled
                ? Localization["Feature.leaderboard.entryUnavailableScheduled"]
                : Localization["Feature.leaderboard.entryUnavailableClosed"];

    protected override Task OnParametersSetAsync()
    {
        if(_loadedTournamentId == Tournament.Id)
            return Task.CompletedTask;

        _loadedTournamentId = Tournament.Id;
        return RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loadCancellation?.Cancel();
        var loadCancellation = new CancellationTokenSource();
        _loadCancellation = loadCancellation;
        var loadGeneration = ++_loadGeneration;

        _isLoading = true;
        _loadError = null;
        try
        {
            var leaderboard = await TournamentService.GetLeaderboardAsync(Tournament.Id, loadCancellation.Token);
            if(!IsCurrentLoad(loadGeneration))
                return;

            _leaderboard = leaderboard;
        }
        catch(OperationCanceledException) when(loadCancellation.IsCancellationRequested)
        {
            // A newer load superseded this request.
        }
        catch(ApiException exception) when(exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if(!IsCurrentLoad(loadGeneration))
                return;

            _loadError = Localization["Feature.leaderboard.notFound"];
        }
        catch(UnauthorizedAccessException)
        {
            if(!IsCurrentLoad(loadGeneration))
                return;

            _loadError = Localization["Feature.leaderboard.unauthorized"];
        }
        catch(Exception)
        {
            if(!IsCurrentLoad(loadGeneration))
                return;

            _loadError = Localization["Feature.leaderboard.loadFailed"];
        }
        finally
        {
            if(IsCurrentLoad(loadGeneration))
            {
                _loadCancellation = null;
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }

            loadCancellation.Dispose();
        }
    }

    private bool IsCurrentLoad(long loadGeneration) =>
        !_isDisposed && loadGeneration == _loadGeneration;

    private void OpenResultEntry() => _isEntryOpen = true;

    private async Task CloseResultEntryAsync(bool changed)
    {
        _isEntryOpen = false;
        if(changed)
        {
            await RefreshAsync();
            if(OnLeaderboardChanged.HasDelegate)
                await OnLeaderboardChanged.InvokeAsync();
        }
    }

    private string GetMetricDescription() => Metric == LeaderboardRankingMetric.HighestScore
        ? Localization["Feature.leaderboard.metricHighestScoreDescription"]
        : Localization["Feature.leaderboard.metricFastestTimeDescription"];

    private string GetResultColumnLabel() => Metric == LeaderboardRankingMetric.HighestScore
        ? Localization["Feature.leaderboard.bestScore"]
        : Localization["Feature.leaderboard.bestTime"];

    public void Dispose()
    {
        _isDisposed = true;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }
}
