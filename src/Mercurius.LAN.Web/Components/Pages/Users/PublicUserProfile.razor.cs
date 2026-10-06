using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Mercurius.LAN.Web.Components.Pages.Users;

public partial class PublicUserProfile
    : IDisposable
{
    [Inject] private IUserClient UserClient { get; set; } = null!;

    [Parameter] public string Username { get; set; } = string.Empty;

    private UserDTO? _profile;
    private PublicProfileMatchSummariesDTO? _matchSummaries;
    private bool _isLoading;
    private bool _hasError;
    private bool _isMatchSummariesLoading;
    private bool _hasMatchSummariesError;
    private CancellationTokenSource? _loadCancellation;
    private bool _disposed;
    private string? _loadedUsername;
    private bool HasLinkedIdentities =>
        !string.IsNullOrWhiteSpace(_profile?.DiscordId) ||
        !string.IsNullOrWhiteSpace(_profile?.SteamId) ||
        !string.IsNullOrWhiteSpace(_profile?.RiotId);
    private string FullName => GetFullName(_profile);
    private string PageTitleText => _profile is null
        ? Localization["General.PublicUser.PageTitle"]
        : Localization.Get("General.PublicUser.NamedPageTitle", FullName);

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
        _profile = null;
        _loadedUsername = null;
        _matchSummaries = null;
        _isMatchSummariesLoading = false;
        _hasMatchSummariesError = false;

        var decodedUsername = Uri.UnescapeDataString(Username ?? string.Empty).Trim();
        try
        {
            if(string.IsNullOrWhiteSpace(decodedUsername))
            {
                _isLoading = false;
                return;
            }

            try
            {
                var profile = await UserClient.GetUserByUsernameAsync(decodedUsername, cancellationToken);
                if(!IsCurrentLoad(cancellation))
                    return;

                _profile = profile;
                _loadedUsername = decodedUsername;
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

            if(!IsCurrentLoad(cancellation) || _hasError || _profile is null)
                return;

            await LoadMatchSummariesAsync(decodedUsername, cancellation);
        }
        finally
        {
            if(ReferenceEquals(_loadCancellation, cancellation))
                _loadCancellation = null;
        }
    }

    private async Task LoadMatchSummariesAsync(
        string username,
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
            var matchSummaries = await UserClient.GetUserMatchSummariesAsync(username, cancellation.Token);
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
        if(_disposed || _profile is null || string.IsNullOrWhiteSpace(_loadedUsername))
            return;

        CancelCurrentLoad();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _matchSummaries = null;
        _hasMatchSummariesError = false;

        try
        {
            await LoadMatchSummariesAsync(_loadedUsername, cancellation);
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

    private static string GetFullName(UserDTO? profile)
    {
        if(profile is null)
            return string.Empty;

        var fullName = $"{profile.Firstname} {profile.Lastname}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? profile.Username ?? string.Empty : fullName;
    }

    private static string GetInitials(UserDTO profile)
    {
        var firstInitial = GetFirstCharacter(profile.Firstname);
        var lastInitial = GetFirstCharacter(profile.Lastname);

        if(!string.IsNullOrWhiteSpace(firstInitial + lastInitial))
            return $"{firstInitial}{lastInitial}".ToUpperInvariant();

        var username = profile.Username;
        if(string.IsNullOrWhiteSpace(username))
            return "?";

        var trimmed = username.Trim();
        if(trimmed.Length == 1)
            return trimmed.ToUpperInvariant();

        return trimmed[..2].ToUpperInvariant();
    }

    private static string GetFirstCharacter(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? string.Empty : trimmed[..1];
    }
}
