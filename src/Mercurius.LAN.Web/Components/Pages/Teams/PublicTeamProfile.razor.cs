using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Mercurius.LAN.Web.Components.Pages.Teams;

public partial class PublicTeamProfile
    : IDisposable
{
    [Inject] private ITeamService TeamService { get; set; } = null!;

    [Parameter] public string TeamName { get; set; } = string.Empty;

    private PublicTeamProfileDTO? _team;
    private PublicProfileMatchSummariesDTO? _matchSummaries;
    private bool _isLoading;
    private bool _hasError;
    private bool _isMatchSummariesLoading;
    private bool _hasMatchSummariesError;
    private CancellationTokenSource? _loadCancellation;
    private bool _disposed;
    private string? _loadedTeamName;
    private int MemberCount => _team?.Members.Count ?? 0;

    protected override async Task OnParametersSetAsync()
    {
        if(_disposed)
            return;

        CancelCurrentLoad();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        var cancellationToken = cancellation.Token;

        _isLoading = true;
        _hasError = false;
        _team = null;
        _loadedTeamName = null;
        _matchSummaries = null;
        _isMatchSummariesLoading = false;
        _hasMatchSummariesError = false;

        var decodedTeamName = Uri.UnescapeDataString(TeamName ?? string.Empty).Trim();
        try
        {
            if(string.IsNullOrWhiteSpace(decodedTeamName))
            {
                _isLoading = false;
                return;
            }

            try
            {
                var team = await TeamService.GetPublicTeamByNameAsync(decodedTeamName, cancellationToken);
                if(!IsCurrentLoad(cancellation))
                    return;

                _team = team;
                _loadedTeamName = decodedTeamName;
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch(Exception)
            {
                if(IsCurrentLoad(cancellation))
                    _hasError = true;
            }
            finally
            {
                if(IsCurrentLoad(cancellation))
                    _isLoading = false;
            }

            if(!IsCurrentLoad(cancellation) || _hasError || _team is null)
                return;

            await LoadMatchSummariesAsync(decodedTeamName, cancellation);
        }
        finally
        {
            if(ReferenceEquals(_loadCancellation, cancellation))
                _loadCancellation = null;
        }
    }

    private Task RetryLoadAsync() => OnParametersSetAsync();

    private async Task LoadMatchSummariesAsync(
        string teamName,
        CancellationTokenSource cancellation)
    {
        if(!IsCurrentLoad(cancellation))
            return;

        var cancellationToken = cancellation.Token;
        _isMatchSummariesLoading = true;
        await NotifyStateChangedAsync(cancellation);
        if(!IsCurrentLoad(cancellation))
            return;

        try
        {
            var matchSummaries = await TeamService.GetPublicTeamMatchSummariesAsync(teamName, cancellation.Token);
            if(!IsCurrentLoad(cancellation))
                return;

            _matchSummaries = matchSummaries ?? new PublicProfileMatchSummariesDTO();
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch(Exception)
        {
            if(IsCurrentLoad(cancellation))
                _hasMatchSummariesError = true;
        }
        finally
        {
            if(IsCurrentLoad(cancellation))
                _isMatchSummariesLoading = false;
        }
    }

    private async Task RetryMatchSummariesAsync()
    {
        if(_disposed || _team is null || string.IsNullOrWhiteSpace(_loadedTeamName))
            return;

        CancelCurrentLoad();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _matchSummaries = null;
        _hasMatchSummariesError = false;

        try
        {
            await LoadMatchSummariesAsync(_loadedTeamName, cancellation);
        }
        finally
        {
            if(ReferenceEquals(_loadCancellation, cancellation))
                _loadCancellation = null;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        CancelCurrentLoad();
        _loadCancellation = null;
    }

    private void CancelCurrentLoad()
    {
        try
        {
            _loadCancellation?.Cancel();
        }
        catch(ObjectDisposedException)
        {
            // The owning async load disposes its source after it completes.
        }
    }

    private bool IsCurrentLoad(CancellationTokenSource cancellation) =>
        !_disposed && ReferenceEquals(_loadCancellation, cancellation) && !cancellation.IsCancellationRequested;

    private async Task NotifyStateChangedAsync(CancellationTokenSource cancellation)
    {
        if(!IsCurrentLoad(cancellation))
            return;

        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch(ObjectDisposedException)
        {
        }
        catch(InvalidOperationException)
        {
        }
    }

    private static string GetMemberInitials(string username)
    {
        if(string.IsNullOrWhiteSpace(username))
            return "?";

        var trimmed = username.Trim();
        if(trimmed.Length == 1)
            return trimmed.ToUpperInvariant();

        return trimmed[..2].ToUpperInvariant();
    }
}
