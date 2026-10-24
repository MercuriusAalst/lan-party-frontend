using System.Net;
using System.Reflection;
using System.Security.Claims;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Components.Layout;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class NavigationIdentityContractTests
{
    [Fact]
    public async Task NavigationStaysNeutralUntilACompletedProfileIsValidated()
    {
        var request = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var userClient = CreateUserClient(_ =>
        {
            requested.TrySetResult();
            return request.Task;
        });
        var principal = PrincipalWithProviderClaims("user-1");
        var menu = CreateMenu(principal, userClient);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            AssertNeutral(menu);

            await menu.StartInteractiveRenderAsync();
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(2));
            AssertNeutral(menu);

            request.SetResult(CompletedProfile("confirmed-player", "/images/confirmed.png"));
            await menu.WaitForAuthenticatedNavigationAsync();

            Assert.Equal("confirmed-player", menu.DisplayName);
            Assert.Equal("confirmed-player", menu.CurrentProfileUsername);
            Assert.Equal("/images/confirmed.png", menu.CurrentProfilePictureUrl);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Theory]
    [InlineData((int)HttpStatusCode.NotFound)]
    [InlineData((int)HttpStatusCode.Gone)]
    [InlineData(0)]
    public async Task UnavailableCurrentProfileKeepsNavigationNeutral(int statusCode)
    {
        var userClient = CreateUserClient(_ => statusCode == 0
            ? Task.FromException<CurrentUserProfileResponse>(new HttpRequestException("offline"))
            : Task.FromException<CurrentUserProfileResponse>(
                new HttpRequestException("profile unavailable", null, (HttpStatusCode)statusCode)));
        var menu = CreateMenu(PrincipalWithProviderClaims("user-1"), userClient);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await menu.WaitForAuthenticatedNavigationAsync();

            AssertNeutral(menu);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Fact]
    public async Task IncompleteProfileNeverSuppliesNavigationIdentity()
    {
        var userClient = CreateUserClient(_ => Task.FromResult(new CurrentUserProfileResponse(
            false,
            Profile("incomplete-player", "/images/incomplete.png"),
            null,
            false)));
        var menu = CreateMenu(PrincipalWithProviderClaims("user-1"), userClient);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await menu.WaitForAuthenticatedNavigationAsync();

            AssertNeutral(menu);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Fact]
    public async Task InconsistentlyDeletedCompletedProfileNeverSuppliesNavigationIdentity()
    {
        var userClient = CreateUserClient(_ => Task.FromResult(new CurrentUserProfileResponse(
            true,
            new UserProfileDTO
            {
                Username = "deleted-player",
                ProfilePictureUrl = "/images/deleted.png",
                IsDeleted = true
            },
            null,
            true)));
        var menu = CreateMenu(PrincipalWithProviderClaims("user-1"), userClient);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await menu.WaitForAuthenticatedNavigationAsync();

            AssertNeutral(menu);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Fact]
    public async Task IdentityChangeClearsPreviousProfileAndIgnoresItsLateResponse()
    {
        var requests = new List<TaskCompletionSource<CurrentUserProfileResponse>>();
        var userClient = CreateUserClient(_ =>
        {
            var request = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(requests)
                requests.Add(request);
            return request.Task;
        });
        var menu = CreateMenu(PrincipalWithProviderClaims("user-1"), userClient);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await WaitForAsync(() => RequestCount(requests) == 1);

            menu.SetAuthenticationState(PrincipalWithProviderClaims("user-2"));
            await menu.ApplyParametersAsync();
            AssertNeutral(menu);
            await menu.StartInteractiveRenderAsync();
            await WaitForAsync(() => RequestCount(requests) == 2);

            RequestAt(requests, 0).SetResult(CompletedProfile("previous-player", "/images/previous.png"));
            await Task.Delay(25);
            AssertNeutral(menu);

            RequestAt(requests, 1).SetResult(CompletedProfile("current-player", "/images/current.png"));
            await menu.WaitForAuthenticatedNavigationAsync();

            Assert.Equal("current-player", menu.DisplayName);
            Assert.Equal("current-player", menu.CurrentProfileUsername);
            Assert.Equal("/images/current.png", menu.CurrentProfilePictureUrl);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Fact]
    public async Task LateNotifierResponseCannotOverwriteANewIdentity()
    {
        var requests = new List<TaskCompletionSource<CurrentUserProfileResponse>>();
        var userClient = CreateUserClient(_ =>
        {
            var request = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(requests)
                requests.Add(request);
            return request.Task;
        });
        var notifier = new CurrentUserProfileNotifier();
        var menu = CreateMenu(PrincipalWithProviderClaims("user-1"), userClient, notifier);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await WaitForAsync(() => RequestCount(requests) == 1);
            RequestAt(requests, 0).SetResult(CompletedProfile("previous-player"));
            await menu.WaitForAuthenticatedNavigationAsync();

            var notifierTask = notifier.NotifyChangedAsync();
            await WaitForAsync(() => RequestCount(requests) == 2);

            menu.SetAuthenticationState(PrincipalWithProviderClaims("user-2"));
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await WaitForAsync(() => RequestCount(requests) == 3);
            RequestAt(requests, 2).SetResult(CompletedProfile("current-player"));
            await menu.WaitForAuthenticatedNavigationAsync();

            RequestAt(requests, 1).SetResult(CompletedProfile("stale-player"));
            await notifierTask;

            Assert.Equal("current-player", menu.DisplayName);
            Assert.Equal("current-player", menu.CurrentProfileUsername);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Fact]
    public async Task ValidNotifierResponseRefreshesTheConfirmedProfile()
    {
        var receivedProfile = false;
        var userClient = CreateUserClient(_ =>
        {
            if(!receivedProfile)
            {
                receivedProfile = true;
                return Task.FromResult(CompletedProfile("old-player", "/images/old.png"));
            }

            return Task.FromResult(CompletedProfile("updated-player", "/images/updated.png"));
        });
        var notifier = new CurrentUserProfileNotifier();
        var menu = CreateMenu(PrincipalWithProviderClaims("user-1"), userClient, notifier);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await menu.WaitForAuthenticatedNavigationAsync();

            await notifier.NotifyChangedAsync();

            Assert.Equal("updated-player", menu.DisplayName);
            Assert.Equal("updated-player", menu.CurrentProfileUsername);
            Assert.Equal("/images/updated.png", menu.CurrentProfilePictureUrl);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidNotifierResponseClearsPreviouslyValidatedProfile(bool failRequest)
    {
        var receivedProfile = false;
        var userClient = CreateUserClient(_ =>
        {
            if(!receivedProfile)
            {
                receivedProfile = true;
                return Task.FromResult(CompletedProfile("confirmed-player", "/images/confirmed.png"));
            }

            return failRequest
                ? Task.FromException<CurrentUserProfileResponse>(new HttpRequestException("offline"))
                : Task.FromResult(new CurrentUserProfileResponse(
                    false,
                    Profile("incomplete-player", "/images/incomplete.png"),
                    null,
                    false));
        });
        var notifier = new CurrentUserProfileNotifier();
        var menu = CreateMenu(PrincipalWithProviderClaims("user-1"), userClient, notifier);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await menu.WaitForAuthenticatedNavigationAsync();
            Assert.Equal("confirmed-player", menu.DisplayName);

            await notifier.NotifyChangedAsync();

            AssertNeutral(menu);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    [Fact]
    public async Task OverlappingNotifierDoesNotStopInitialNavigationServices()
    {
        var requests = new List<TaskCompletionSource<CurrentUserProfileResponse>>();
        var userClient = CreateUserClient(_ =>
        {
            var request = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(requests)
                requests.Add(request);
            return request.Task;
        });
        var notificationService = new TrackingNotificationService();
        var realtimeService = new TrackingRealtimeService();
        var notifier = new CurrentUserProfileNotifier();
        var menu = CreateMenu(
            PrincipalWithProviderClaims("user-1"),
            userClient,
            notifier,
            notificationService,
            realtimeService);

        try
        {
            menu.StartInitialization();
            await menu.ApplyParametersAsync();
            await menu.StartInteractiveRenderAsync();
            await WaitForAsync(() => RequestCount(requests) == 1);

            var notifierTask = notifier.NotifyChangedAsync();
            await WaitForAsync(() => RequestCount(requests) == 2);

            RequestAt(requests, 0).SetResult(CompletedProfile("superseded-player"));
            await menu.WaitForAuthenticatedNavigationAsync();

            Assert.Equal(1, notificationService.RefreshCalls);
            Assert.Equal(1, realtimeService.StartCalls);
            AssertNeutral(menu);

            RequestAt(requests, 1).SetResult(CompletedProfile("notifier-player"));
            await notifierTask;

            Assert.Equal("notifier-player", menu.DisplayName);
        }
        finally
        {
            await menu.DisposeForTestAsync();
        }
    }

    private static TestNavMenu CreateMenu(
        ClaimsPrincipal principal,
        IUserClient userClient,
        CurrentUserProfileNotifier? notifier = null,
        TrackingNotificationService? notificationService = null,
        TrackingRealtimeService? realtimeService = null)
    {
        var menu = new TestNavMenu();
        SetProperty(menu, "UserClient", userClient);
        SetProperty(menu, "NotificationService", notificationService ?? new TrackingNotificationService());
        SetProperty(menu, "TeamRealtimeService", realtimeService ?? new TrackingRealtimeService());
        SetProperty(menu, "CurrentUserProfileNotifier", notifier ?? new CurrentUserProfileNotifier());
        SetProperty(menu, "AuthenticationStateTask", Task.FromResult(new AuthenticationState(principal)));
        return menu;
    }

    private static ClaimsPrincipal PrincipalWithProviderClaims(string identityKey) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, identityKey),
            new Claim("sub", "google-oauth2|provider-subject"),
            new Claim("preferred_username", "provider-nickname"),
            new Claim("nickname", "provider-nickname"),
            new Claim("username", "provider-username"),
            new Claim(ClaimTypes.Name, "Provider Name"),
            new Claim(ClaimTypes.Email, "provider@example.test")
        ],
        authenticationType: "test"));

    private static CurrentUserProfileResponse CompletedProfile(string username, string? pictureUrl = null) =>
        new(true, Profile(username, pictureUrl), null, true);

    private static UserProfileDTO Profile(string username, string? pictureUrl) =>
        new() { Username = username, ProfilePictureUrl = pictureUrl };

    private static void AssertNeutral(TestNavMenu menu)
    {
        Assert.Equal(TestLocalizationService.Instance["nav.account"], menu.DisplayName);
        Assert.DoesNotContain("provider", menu.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Null(menu.CurrentProfileUsername);
        Assert.Null(menu.CurrentProfilePictureUrl);
    }

    private static int RequestCount(List<TaskCompletionSource<CurrentUserProfileResponse>> requests)
    {
        lock(requests)
            return requests.Count;
    }

    private static TaskCompletionSource<CurrentUserProfileResponse> RequestAt(
        List<TaskCompletionSource<CurrentUserProfileResponse>> requests,
        int index)
    {
        lock(requests)
            return requests[index];
    }

    private static IUserClient CreateUserClient(Func<object?[]?, Task<CurrentUserProfileResponse>> handler)
    {
        var proxy = DispatchProxy.Create<IUserClient, UserClientProxy>();
        ((UserClientProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var property = FindProperty(instance.GetType(), propertyName)
            ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
        property.SetValue(instance, value);
    }

    private static PropertyInfo? FindProperty(Type? type, string propertyName)
    {
        while(type is not null)
        {
            var property = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if(property is not null)
                return property;
            type = type.BaseType;
        }

        return null;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while(!condition())
        {
            if(DateTime.UtcNow >= timeout)
                throw new TimeoutException("Timed out waiting for the navigation state.");
            await Task.Delay(10);
        }
    }

    private class UserClientProxy : DispatchProxy
    {
        public Func<object?[]?, Task<CurrentUserProfileResponse>> Handler { get; set; } = _ =>
            throw new InvalidOperationException("No profile response was configured.");

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name != nameof(IUserClient.GetCurrentUserProfileAsync))
                throw new NotSupportedException(targetMethod?.Name);
            return Handler(args);
        }
    }

    private sealed class TestNavMenu : NavMenu
    {
        private readonly TestRenderer _renderer = new();

        public TestNavMenu()
        {
            _renderer.Attach(this);
            SetProperty(this, "Localization", TestLocalizationService.Instance);
            SetProperty(this, "NavigationManager", new RecordingNavigationManager("https://example.test/"));
        }

        public void StartInitialization() => base.OnInitialized();

        public Task ApplyParametersAsync() => base.OnParametersSetAsync();

        public Task StartInteractiveRenderAsync() => base.OnAfterRenderAsync(firstRender: true);

        public Task WaitForAuthenticatedNavigationAsync() =>
            AuthenticatedNavigationTask ?? throw new InvalidOperationException("Authenticated navigation has not started.");

        public new string? CurrentProfileUsername => base.CurrentProfileUsername;

        public new string? CurrentProfilePictureUrl => base.CurrentProfilePictureUrl;

        public string DisplayName => (string)(typeof(NavMenu)
            .GetMethod("GetDisplayName", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(this, null) ?? throw new MissingMethodException(typeof(NavMenu).FullName, "GetDisplayName"));

        public void SetAuthenticationState(ClaimsPrincipal principal) =>
            SetProperty(this, "AuthenticationStateTask", Task.FromResult(new AuthenticationState(principal)));

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
        }

        public async ValueTask DisposeForTestAsync()
        {
            await base.DisposeAsync();
            _renderer.Dispose();
        }

    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public List<string> Navigations { get; } = [];

        public RecordingNavigationManager(string uri) => Initialize("https://example.test/", uri);

        protected override void NavigateToCore(string uri, NavigationOptions options) => Navigations.Add(uri);
    }

    private class NoopNotificationService : ITeamNotificationService
    {
        public event Func<Task>? Changed;
        public event Func<Guid, Task>? RosterDecisionChanged;
        public IReadOnlyList<TeamNotificationItem> Notifications => [];
        public int UnreadCount => 0;
        public virtual Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RespondToRosterSelectionAsync(string notificationId, bool accept, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkAllReadAsync() => Task.CompletedTask;
        public Task DismissAsync(string id) => Task.CompletedTask;
    }

    private sealed class TrackingNotificationService : NoopNotificationService
    {
        public int RefreshCalls { get; private set; }

        public override Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingRealtimeService : ITeamRealtimeService
    {
        public event Func<Task>? TeamStateInvalidated;
        public bool IsConnected => false;
        public int StartCalls { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.CompletedTask;
        }

        public Task JoinTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestRenderer : Renderer
    {
        public TestRenderer()
            : base(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
        {
        }

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public void Attach(IComponent component) => AssignRootComponentId(component);

        protected override void HandleException(Exception exception) => throw exception;

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
}
