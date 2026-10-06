using Mercurius.LAN.Web.DTOs.Search;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Services;
#if INCLUDE_MOCK_BACKEND
using Mercurius.LAN.Web.Options;
using Microsoft.Extensions.Options;
#endif
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace Mercurius.LAN.Web.Components.Layout;

public partial class NavMenu : IAsyncDisposable
{
    private const int SearchDebounceMilliseconds = 300;
    private const int MinimumSearchQueryLength = 3;
    private const string SearchContainerElementId = "global-nav-search-container";
    private const string AccountMenuContainerElementId = "account-nav-menu-container";
    private const string AdminMenuContainerElementId = "admin-nav-menu-container";
    private const string AdminMenuTriggerElementId = "admin-nav-menu-trigger";
    private const string AdminMenuElementId = "admin-nav-menu";

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;
#if INCLUDE_MOCK_BACKEND
    [Inject]
    private IOptions<MockBackendOptions> MockBackendOptions { get; set; } = null!;
#endif
    [Inject]
    private IGlobalSearchService GlobalSearchService { get; set; } = null!;
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject]
    private IUserClient UserClient { get; set; } = null!;
    [Inject]
    private ITeamNotificationService NotificationService { get; set; } = null!;
    [Inject]
    private ITeamRealtimeService TeamRealtimeService { get; set; } = null!;
    [Inject]
    private ITeamService TeamService { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    private bool _isUserMenuVisible = false;
    private bool _isDropdownVisible = false;
    private bool _isInfoMenuVisible = false;
    private bool _isNotificationMenuVisible = false;
    private bool _isAdmin;
    private string _searchQuery = string.Empty;
    private List<GlobalSearchResultDTO> _searchResults = [];
    private bool _isSearchLoading;
    private bool _hasSearchError;
    private bool _isSearchDropdownVisible;
    private int _highlightedSearchIndex = -1;
    private string? _searchNextCursor;
    private bool _searchHasMore;
    private long _searchRequestVersion;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private IJSObjectReference? _searchOutsideClickListener;
    private IJSObjectReference? _accountMenuOutsideClickListener;
    private IJSObjectReference? _adminMenuOutsideClickListener;
    private DotNetObjectReference<NavMenu>? _searchOutsideClickReference;
    private ElementReference _adminMenuTrigger;
    private string? _loadedIdentityKey;
    private string? _currentProfileUsername;
    private readonly CancellationTokenSource _lifetimeCancellationTokenSource = new();
    private CancellationTokenSource? _authenticatedNavigationCancellationTokenSource;
    private Task? _authenticatedNavigationTask;
    private long _authenticatedNavigationVersion;
    private string? _pendingAuthenticatedNavigationIdentityKey;
    private bool _disposed;
    private readonly HashSet<string> _pendingNotificationActionIds = [];
    private string? _notificationActionError;

    [Parameter]
    public EventCallback OnNavigationSelected { get; set; }

    private string LoginHref => $"/account/login?returnUrl={Uri.EscapeDataString(GetCurrentRelativeUrl())}";
    private string RegisterHref => $"/account/register?returnUrl={Uri.EscapeDataString(GetCurrentRelativeUrl())}";
#if INCLUDE_MOCK_BACKEND
    private string MockAdminLoginHref => $"/account/login?persona=admin&returnUrl={Uri.EscapeDataString("/admin/sponsors")}";
    private string MockUserLoginHref => $"/account/login?persona=user&returnUrl={Uri.EscapeDataString("/tournaments")}";
    private bool IsMockBackendEnabled => MockBackendOptions.Value.Enabled;
#endif
    private bool ShouldShowInteractionOverlay => _isUserMenuVisible || _isDropdownVisible || _isInfoMenuVisible || _isNotificationMenuVisible;
    private bool HasSearchResults => _searchResults.Count > 0;
    private int NotificationCount => NotificationService.UnreadCount;

    protected override void OnInitialized()
    {
        NotificationService.Changed += HandleNotificationsChangedAsync;
        TeamRealtimeService.TeamStateInvalidated += RefreshNotificationsFromSignalAsync;
    }

    protected override async Task OnParametersSetAsync()
    {
        if(_disposed || AuthenticationStateTask == null)
            return;

        AuthenticationState authState;
        try
        {
            authState = await AuthenticationStateTask;
        }
        catch(OperationCanceledException) when(_disposed)
        {
            return;
        }

        if(_disposed)
            return;

        var user = authState.User;
        _isAdmin = user.IsInRole("admin");
        if(user.Identity?.IsAuthenticated != true)
        {
            CancelAuthenticatedNavigation();
            _pendingAuthenticatedNavigationIdentityKey = null;
            _loadedIdentityKey = null;
            _currentProfileUsername = null;
            return;
        }

        var identityKey = GetIdentityKey(user);
        if(string.Equals(identityKey, _loadedIdentityKey, StringComparison.Ordinal))
            return;

        _loadedIdentityKey = identityKey;
        _currentProfileUsername = null;
        CancelAuthenticatedNavigation();
        _pendingAuthenticatedNavigationIdentityKey = identityKey;
    }

    private void StartAuthenticatedNavigation(string identityKey)
    {
        if(_disposed || !string.Equals(identityKey, _loadedIdentityKey, StringComparison.Ordinal))
            return;

        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellationTokenSource.Token);
        _authenticatedNavigationCancellationTokenSource = cancellationTokenSource;
        var navigationVersion = ++_authenticatedNavigationVersion;
        _authenticatedNavigationTask = LoadAuthenticatedNavigationAsync(identityKey, navigationVersion, cancellationTokenSource);
    }

    private async Task LoadAuthenticatedNavigationAsync(
        string identityKey,
        long navigationVersion,
        CancellationTokenSource cancellationTokenSource)
    {
        var cancellationToken = cancellationTokenSource.Token;
        try
        {
            var profile = await UserClient.GetCurrentUserProfileAsync();
            if(!IsCurrentAuthenticatedNavigation(identityKey, navigationVersion, cancellationToken))
                return;

            _currentProfileUsername = profile.User?.Username?.Trim();
            await NotificationService.RefreshAsync(cancellationToken);
            if(!IsCurrentAuthenticatedNavigation(identityKey, navigationVersion, cancellationToken))
                return;

            await TeamRealtimeService.StartAsync(cancellationToken);
            if(!IsCurrentAuthenticatedNavigation(identityKey, navigationVersion, cancellationToken))
                return;

            await InvokeAsync(() =>
            {
                if(IsCurrentAuthenticatedNavigation(identityKey, navigationVersion, cancellationToken))
                    StateHasChanged();
            });
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested || _disposed)
        {
        }
        catch(Exception)
        {
        }
        finally
        {
            if(ReferenceEquals(_authenticatedNavigationCancellationTokenSource, cancellationTokenSource))
            {
                _authenticatedNavigationCancellationTokenSource = null;
                cancellationTokenSource.Dispose();
            }
        }
    }

    private bool IsCurrentAuthenticatedNavigation(
        string identityKey,
        long navigationVersion,
        CancellationToken cancellationToken) =>
        !_disposed &&
        !cancellationToken.IsCancellationRequested &&
        navigationVersion == _authenticatedNavigationVersion &&
        string.Equals(identityKey, _loadedIdentityKey, StringComparison.Ordinal);

    private void CancelAuthenticatedNavigation()
    {
        _authenticatedNavigationVersion++;
        var cancellationTokenSource = _authenticatedNavigationCancellationTokenSource;
        _authenticatedNavigationCancellationTokenSource = null;
        if(cancellationTokenSource is null)
            return;

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(_disposed)
            return;

        var pendingIdentityKey = _pendingAuthenticatedNavigationIdentityKey;
        _pendingAuthenticatedNavigationIdentityKey = null;
        if(pendingIdentityKey is not null)
            StartAuthenticatedNavigation(pendingIdentityKey);

        if(_isSearchDropdownVisible && _searchOutsideClickListener == null)
        {
            _searchOutsideClickReference ??= DotNetObjectReference.Create(this);
            _searchOutsideClickListener = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "addNavSearchOutsideClickListener",
                SearchContainerElementId,
                _searchOutsideClickReference);
        }
        else if(!_isSearchDropdownVisible && _searchOutsideClickListener != null)
        {
            await DisposeSearchOutsideClickListenerAsync();
        }

        if((_isUserMenuVisible || _isNotificationMenuVisible) && _accountMenuOutsideClickListener == null)
        {
            _searchOutsideClickReference ??= DotNetObjectReference.Create(this);
            _accountMenuOutsideClickListener = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "addNavMenuOutsideClickListener",
                AccountMenuContainerElementId,
                _searchOutsideClickReference);
        }
        else if(!_isUserMenuVisible && !_isNotificationMenuVisible && _accountMenuOutsideClickListener != null)
        {
            await DisposeAccountMenuOutsideClickListenerAsync();
        }

        if(_isDropdownVisible && _adminMenuOutsideClickListener == null)
        {
            _searchOutsideClickReference ??= DotNetObjectReference.Create(this);
            _adminMenuOutsideClickListener = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "addNavAdminMenuListener",
                AdminMenuContainerElementId,
                _searchOutsideClickReference);
        }
        else if(!_isDropdownVisible && _adminMenuOutsideClickListener != null)
        {
            await DisposeAdminMenuOutsideClickListenerAsync();
        }
    }

    private async Task BeginLogin()
    {
        CloseAllTemporarySurfaces(clearSearchResults: true, clearSearchQuery: true);
        await OnNavigationSelected.InvokeAsync();
        NavigationManager.NavigateTo(LoginHref, forceLoad: true);
    }

    private async Task BeginRegistration()
    {
        CloseAllTemporarySurfaces(clearSearchResults: true, clearSearchQuery: true);
        await OnNavigationSelected.InvokeAsync();
        NavigationManager.NavigateTo(RegisterHref, forceLoad: true);
    }

    private async Task Logout()
    {
        var returnUrl = LocalReturnUrlHelper.GetSafeLogoutReturnUrl(GetCurrentRelativeUrl());
        CloseAllTemporarySurfaces(clearSearchResults: true, clearSearchQuery: true);
        await OnNavigationSelected.InvokeAsync();
        NavigationManager.NavigateTo($"/account/logout?returnUrl={Uri.EscapeDataString(returnUrl)}", true);
    }

    private void ToggleUserMenu()
    {
        var shouldOpen = !_isUserMenuVisible;
        CloseAllTemporarySurfaces();
        _isUserMenuVisible = shouldOpen;
    }

    private void ToggleNotificationMenu()
    {
        var shouldOpen = !_isNotificationMenuVisible;
        CloseAllTemporarySurfaces();
        _isNotificationMenuVisible = shouldOpen;
    }

    private void HandleOutsideClick()
    {
        CloseAllTemporarySurfaces();
    }

    private void HandleNavShellClick()
    {
        if(_isSearchDropdownVisible)
            CloseSearchDropdown(clearResults: false, clearQuery: false);
    }

    private void ToggleDropdown()
    {
        var shouldOpen = !_isDropdownVisible;
        CloseAllTemporarySurfaces();
        _isDropdownVisible = shouldOpen;
    }

    private void ToggleInfoMenu()
    {
        var shouldOpen = !_isInfoMenuVisible;
        CloseAllTemporarySurfaces();
        _isInfoMenuVisible = shouldOpen;
    }

    private void CloseAllTemporarySurfaces(bool clearSearchResults = false, bool clearSearchQuery = false)
    {
        _isDropdownVisible = false;
        _isUserMenuVisible = false;
        _isInfoMenuVisible = false;
        _isNotificationMenuVisible = false;
        CloseSearchDropdown(clearSearchResults, clearSearchQuery);
    }

    private async Task HandleNavigationClicked()
    {
        CloseAllTemporarySurfaces(clearSearchResults: true, clearSearchQuery: true);
        await OnNavigationSelected.InvokeAsync();
    }

    private async Task HandleSearchInputAsync(ChangeEventArgs args)
    {
        _searchQuery = args.Value?.ToString() ?? string.Empty;
        _highlightedSearchIndex = -1;
        _hasSearchError = false;

        var trimmedQuery = _searchQuery.Trim();
        CancelPendingSearch();

        if(trimmedQuery.Length < MinimumSearchQueryLength)
        {
            CloseSearchDropdown(clearResults: true, clearQuery: false);
            return;
        }

        _isDropdownVisible = false;
        _isUserMenuVisible = false;
        _isInfoMenuVisible = false;
        _isNotificationMenuVisible = false;
        _isSearchLoading = true;
        _isSearchDropdownVisible = true;
        _searchResults = [];

        var requestVersion = ++_searchRequestVersion;
        _searchCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _searchCancellationTokenSource.Token;

        try
        {
            await Task.Delay(SearchDebounceMilliseconds, cancellationToken);
            var response = await GlobalSearchService.SearchAsync(trimmedQuery, cancellationToken);

            if(cancellationToken.IsCancellationRequested || requestVersion != _searchRequestVersion)
                return;

            _searchResults = response.Results.ToList();
            _searchNextCursor = response.NextCursor;
            _searchHasMore = response.HasMore;
            _isSearchLoading = false;
            _hasSearchError = false;
            _isSearchDropdownVisible = true;
        }
        catch(OperationCanceledException)
        {
        }
        catch(Exception)
        {
            if(cancellationToken.IsCancellationRequested || requestVersion != _searchRequestVersion)
                return;

            _searchResults = [];
            _isSearchLoading = false;
            _hasSearchError = true;
            _isSearchDropdownVisible = true;
        }
    }

    private void HandleSearchFocus()
    {
        if(_searchQuery.Trim().Length < MinimumSearchQueryLength)
            return;

        _isDropdownVisible = false;
        _isUserMenuVisible = false;
        _isInfoMenuVisible = false;
        _isNotificationMenuVisible = false;
        _isSearchDropdownVisible = true;
    }

    private async Task HandleSearchKeyDownAsync(KeyboardEventArgs args)
    {
        if(args.Key == "Escape")
        {
            CloseSearchDropdown(clearResults: false, clearQuery: false);
            return;
        }

        if(!_isSearchDropdownVisible || _isSearchLoading || _hasSearchError || _searchResults.Count == 0)
            return;

        if(args.Key == "ArrowDown")
        {
            _highlightedSearchIndex = _highlightedSearchIndex < _searchResults.Count - 1
                ? _highlightedSearchIndex + 1
                : 0;
            return;
        }

        if(args.Key == "ArrowUp")
        {
            _highlightedSearchIndex = _highlightedSearchIndex > 0
                ? _highlightedSearchIndex - 1
                : _searchResults.Count - 1;
            return;
        }

        if(args.Key == "Enter" &&
           _highlightedSearchIndex >= 0 &&
           _highlightedSearchIndex < _searchResults.Count)
        {
            await SelectSearchResultAsync(_searchResults[_highlightedSearchIndex]);
        }
    }

    private async Task SelectSearchResultAsync(GlobalSearchResultDTO result)
    {
        if(result.Type == GlobalSearchResultType.User && !_isAdmin)
            return;

        var destination = BuildSearchDestination(result);
        if(string.IsNullOrWhiteSpace(destination))
            return;

        CloseAllTemporarySurfaces(clearSearchResults: true, clearSearchQuery: true);
        await OnNavigationSelected.InvokeAsync();
        NavigationManager.NavigateTo(destination);
    }

    private bool IsSearchResultDisabled(GlobalSearchResultDTO result) =>
        result.Type == GlobalSearchResultType.User && !_isAdmin;

    private Task ClearSearchInputAsync()
    {
        _searchQuery = string.Empty;
        CloseSearchDropdown(clearResults: true, clearQuery: false);
        return Task.CompletedTask;
    }

    private void SetSearchHighlight(int index)
    {
        _highlightedSearchIndex = index;
    }

    [JSInvokable]
    public async Task CloseDropdown()
    {
        if(!_isSearchDropdownVisible)
            return;

        await InvokeAsync(() =>
        {
            CloseSearchDropdown(clearResults: false, clearQuery: false);
            StateHasChanged();
        });
    }

    [JSInvokable]
    public async Task CloseAccountDropdowns()
    {
        if(!_isUserMenuVisible && !_isNotificationMenuVisible)
            return;

        await InvokeAsync(() =>
        {
            _isUserMenuVisible = false;
            _isNotificationMenuVisible = false;
            StateHasChanged();
        });
    }

    [JSInvokable]
    public async Task CloseAdminDropdown(bool restoreFocus = false)
    {
        if(!_isDropdownVisible)
            return;

        await InvokeAsync(async () =>
        {
            _isDropdownVisible = false;
            StateHasChanged();

            if(restoreFocus)
                await _adminMenuTrigger.FocusAsync();
        });
    }

    private void CloseSearchDropdown(bool clearResults, bool clearQuery)
    {
        CancelPendingSearch();
        _isSearchDropdownVisible = false;
        _isSearchLoading = false;
        _hasSearchError = false;
        _highlightedSearchIndex = -1;
        _searchNextCursor = null;
        _searchHasMore = false;

        if(clearResults)
            _searchResults = [];

        if(clearQuery)
            _searchQuery = string.Empty;
    }

    private void CancelPendingSearch()
    {
        _searchRequestVersion++;
        if(_searchCancellationTokenSource == null)
            return;

        _searchCancellationTokenSource.Cancel();
        _searchCancellationTokenSource.Dispose();
        _searchCancellationTokenSource = null;
    }

    private string GetAdminButtonClass() => GetUtilityButtonClass(_isDropdownVisible);

    private string GetInfoButtonClass()
    {
        var classes = "brand-nav-link info-nav-button text-center md:text-left";
        return _isInfoMenuVisible || IsInfoPage ? $"{classes} brand-nav-link--active" : classes;
    }

    private string GetUserButtonClass() => $"{GetUtilityButtonClass(_isUserMenuVisible)} user-button";

    private string GetNotificationButtonClass()
    {
        var classes = "brand-utility-button nav-widget-notification-button";
        return _isNotificationMenuVisible ? $"{classes} brand-utility-button--open" : classes;
    }

    private string GetSearchContainerClass()
    {
        var classes = "brand-nav-search";
        return _isSearchDropdownVisible ? $"{classes} brand-nav-search--open" : classes;
    }

    private string GetSearchInputShellClass()
    {
        var classes = "nav-search-input-shell";
        return _isSearchDropdownVisible ? $"{classes} nav-search-input-shell--open" : classes;
    }

    private bool IsInfoPage => NavigationManager.ToBaseRelativePath(NavigationManager.Uri).StartsWith("info", StringComparison.OrdinalIgnoreCase);

    private static string GetAriaExpanded(bool isExpanded) => isExpanded ? "true" : "false";

    private static string GetUtilityButtonClass(bool isOpen)
    {
        var classes = "brand-utility-button w-full justify-center md:w-auto";
        return isOpen ? $"{classes} brand-utility-button--open" : classes;
    }

    private string GetCurrentRelativeUrl()
    {
        var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);

        return string.IsNullOrWhiteSpace(relativePath)
            ? "/"
            : $"/{relativePath}";
    }

    private static string GetNotificationItemClass(TeamNotificationItem notification)
    {
        return notification.IsRead
            ? "nav-notification-item"
            : "nav-notification-item nav-notification-item--unread";
    }

    private static string? BuildSearchDestination(GlobalSearchResultDTO result)
    {
        return result.Type switch
        {
            GlobalSearchResultType.User when !string.IsNullOrWhiteSpace(result.Username) =>
                $"/users/{Uri.EscapeDataString(result.Username)}",
            GlobalSearchResultType.Team when !string.IsNullOrWhiteSpace(result.TeamName) =>
                $"/teams/{Uri.EscapeDataString(result.TeamName)}",
            GlobalSearchResultType.Tournament when result.TournamentId.HasValue =>
                $"/tournaments/{result.TournamentId.Value}",
            _ => null
        };
    }

    private string GetDisplayName(ClaimsPrincipal user)
    {
        return _currentProfileUsername
            ?? GetUsernameClaim(user)
            ?? user.Identity?.Name
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst("email")?.Value
            ?? Localization["nav.account"];
    }

    private string GetUserMenuAriaLabel(ClaimsPrincipal user)
    {
        var displayName = GetDisplayName(user);

        return Localization.Get("nav.accountMenu", displayName);
    }

    private string GetNotificationAriaLabel()
    {
        return NotificationCount == 0
            ? Localization["nav.teamNotifications"]
            : Localization.Get("nav.teamNotificationsUnread", NotificationCount);
    }

    private static string GetIdentityKey(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.Identity?.Name
            ?? "authenticated";
    }

    private static string? GetUsernameClaim(ClaimsPrincipal user)
    {
        return user.FindFirst("preferred_username")?.Value?.Trim()
            ?? user.FindFirst("nickname")?.Value?.Trim()
            ?? user.FindFirst("username")?.Value?.Trim();
    }

    private async ValueTask DisposeSearchOutsideClickListenerAsync()
    {
        var listener = _searchOutsideClickListener;
        _searchOutsideClickListener = null;

        if(listener == null)
            return;

        try
        {
            await listener.InvokeVoidAsync("dispose");
            await listener.DisposeAsync();
        }
        catch(JSDisconnectedException)
        {
        }
    }

    private async ValueTask DisposeAccountMenuOutsideClickListenerAsync()
    {
        var listener = _accountMenuOutsideClickListener;
        _accountMenuOutsideClickListener = null;

        if(listener == null)
            return;

        try
        {
            await listener.InvokeVoidAsync("dispose");
            await listener.DisposeAsync();
        }
        catch(JSDisconnectedException)
        {
        }
    }

    private async ValueTask DisposeAdminMenuOutsideClickListenerAsync()
    {
        var listener = _adminMenuOutsideClickListener;
        _adminMenuOutsideClickListener = null;

        if(listener == null)
            return;

        try
        {
            await listener.InvokeVoidAsync("dispose");
            await listener.DisposeAsync();
        }
        catch(JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        CancelAuthenticatedNavigation();
        _lifetimeCancellationTokenSource.Cancel();
        NotificationService.Changed -= HandleNotificationsChangedAsync;
        TeamRealtimeService.TeamStateInvalidated -= RefreshNotificationsFromSignalAsync;
        CancelPendingSearch();
        await DisposeSearchOutsideClickListenerAsync();
        await DisposeAccountMenuOutsideClickListenerAsync();
        await DisposeAdminMenuOutsideClickListenerAsync();
        _searchOutsideClickReference?.Dispose();
        _lifetimeCancellationTokenSource.Dispose();
    }

    private async Task MarkNotificationsReadAsync()
    {
        await NotificationService.MarkAllReadAsync();
    }

    private async Task DismissNotificationAsync(string id)
    {
        await NotificationService.DismissAsync(id);
    }

    private bool IsNotificationActionPending(string notificationId) =>
        _pendingNotificationActionIds.Contains(notificationId);

    private async Task RespondToInviteNotificationAsync(TeamNotificationItem notification, bool accept)
    {
        if(notification.InviteId is not Guid inviteId || !_pendingNotificationActionIds.Add(notification.Id))
            return;

        _notificationActionError = null;
        try
        {
            await TeamService.RespondToInviteAsync(inviteId, accept);
            await NotificationService.RefreshAsync();
        }
        catch(Exception)
        {
            _notificationActionError = Localization["nav.notificationActionFailed"];
        }
        finally
        {
            _pendingNotificationActionIds.Remove(notification.Id);
        }
    }

    private async Task RespondToRosterNotificationAsync(TeamNotificationItem notification, bool accept)
    {
        if(!_pendingNotificationActionIds.Add(notification.Id))
            return;

        _notificationActionError = null;
        try
        {
            await NotificationService.RespondToRosterSelectionAsync(
                notification.Id,
                accept,
                _lifetimeCancellationTokenSource.Token);
        }
        catch(OperationCanceledException) when(_disposed)
        {
        }
        catch(Exception)
        {
            _notificationActionError = Localization["nav.rosterActionFailed"];
            try
            {
                await NotificationService.RefreshAsync(_lifetimeCancellationTokenSource.Token);
            }
            catch(Exception)
            {
            }
        }
        finally
        {
            _pendingNotificationActionIds.Remove(notification.Id);
        }
    }

    private Task HandleNotificationsChangedAsync()
    {
        if(_disposed)
            return Task.CompletedTask;

        return InvokeAsync(() =>
        {
            if(!_disposed)
                StateHasChanged();
        });
    }

    private async Task RefreshNotificationsFromSignalAsync()
    {
        if(_disposed)
            return;

        try
        {
            await NotificationService.RefreshAsync(_lifetimeCancellationTokenSource.Token);
        }
        catch(OperationCanceledException) when(_disposed)
        {
        }
        catch(Exception)
        {
        }
    }
}
