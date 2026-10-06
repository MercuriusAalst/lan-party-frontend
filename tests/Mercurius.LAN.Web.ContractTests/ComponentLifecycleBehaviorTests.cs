using System.Reflection;
using System.Security.Claims;
using System.Net;
using System.Text.Encodings.Web;
using Auth0.AspNetCore.Authentication;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Components.Auth;
using Mercurius.LAN.Web.Components.Layout;
using Mercurius.LAN.Web.Components.Pages;
using Mercurius.LAN.Web.Components.Pages.Teams;
using Mercurius.LAN.Web.Components.Pages.Tournaments;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ComponentLifecycleBehaviorTests
{
    [Fact]
    public async Task HomeRendersSponsorsBeforePendingTournamentRequestCompletes()
    {
        var tournaments = new TaskCompletionSource<List<Tournament>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tournamentService = CreateProxy<ITournamentService>((method, _) =>
            method.Name == nameof(ITournamentService.GetTournamentsAsync)
                ? tournaments.Task
                : throw new NotSupportedException(method.Name));
        var sponsorService = CreateProxy<ISponsorService>((method, _) =>
            method.Name == nameof(ISponsorService.GetSponsorsAsync)
                ? Task.FromResult<IEnumerable<Sponsor>>(Array.Empty<Sponsor>())
                : throw new NotSupportedException(method.Name));
        var component = new TestHome();

        SetProperty(component, "TournamentService", tournamentService);
        SetProperty(component, "SponsorService", sponsorService);
        SetProperty(component, "EventOptions", Microsoft.Extensions.Options.Options.Create(new LanEventOptions()));

        await component.StartInitialRenderAsync();
        await WaitForAsync(() => component.RenderCount >= 2);

        Assert.False(tournaments.Task.IsCompleted);

        await component.DisposeAsync();
        tournaments.SetResult([]);
        await Task.Delay(25);

        Assert.Equal(2, component.RenderCount);
    }

    [Fact]
    public async Task TournamentOverviewRendersSponsorsBeforePendingTournamentRequestCompletes()
    {
        var tournaments = new TaskCompletionSource<List<Tournament>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tournamentService = CreateProxy<ITournamentService>((method, _) =>
            method.Name == nameof(ITournamentService.GetTournamentsAsync)
                ? tournaments.Task
                : throw new NotSupportedException(method.Name));
        var sponsorService = CreateProxy<ISponsorService>((method, _) =>
            method.Name == nameof(ISponsorService.GetSponsorsAsync)
                ? Task.FromResult<IEnumerable<Sponsor>>(Array.Empty<Sponsor>())
                : throw new NotSupportedException(method.Name));
        var component = new TestTournamentsOverview();

        SetProperty(component, "TournamentService", tournamentService);
        SetProperty(component, "SponsorService", sponsorService);

        await component.StartInitialRenderAsync();
        await WaitForAsync(() => component.RenderCount >= 2);

        Assert.False(tournaments.Task.IsCompleted);

        await component.DisposeAsync();
        tournaments.SetResult([]);
        await Task.Delay(25);

        Assert.Equal(2, component.RenderCount);
    }

    [Fact]
    public async Task ManageTeamsDoesNotStartRealtimeAfterDisposalCancelsPendingInitialization()
    {
        var summaryRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var summary = new TaskCompletionSource<CurrentUserTeamSummaryDTO>(TaskCreationOptions.RunContinuationsAsynchronously);
        var teamService = CreateProxy<ITeamService>((method, args) =>
        {
            if(method.Name != nameof(ITeamService.GetCurrentUserTeamSummaryAsync))
                throw new NotSupportedException(method.Name);

            summaryRequested.SetResult(true);
            var cancellationToken = args is { Length: > 0 } && args[0] is CancellationToken token
                ? token
                : CancellationToken.None;
            return summary.Task.WaitAsync(cancellationToken);
        });
        var realtimeService = new TrackingRealtimeService();
        var component = new TestManageTeams();

        SetProperty(component, "TeamService", teamService);
        SetProperty(component, "NotificationService", new NoopNotificationService());
        SetProperty(component, "RealtimeService", realtimeService);

        component.StartInitialization();
        await summaryRequested.Task;
        await component.DisposeAsync();

        summary.SetResult(new CurrentUserTeamSummaryDTO());
        await Task.Delay(25);

        Assert.Equal(0, realtimeService.StartCalls);
    }

    [Fact]
    public async Task ManageTeamsRendersRestSummaryWhileRealtimeStartupIsPending()
    {
        var teamId = Guid.NewGuid();
        var summaryRequests = 0;
        var summary = new CurrentUserTeamSummaryDTO
        {
            CaptainedTeams = [new TeamManagementSummaryDTO { Id = teamId, Name = "Current team" }]
        };
        var teamService = CreateProxy<ITeamService>((method, _) =>
        {
            if(method.Name != nameof(ITeamService.GetCurrentUserTeamSummaryAsync))
                throw new NotSupportedException(method.Name);

            Interlocked.Increment(ref summaryRequests);
            return Task.FromResult(summary);
        });
        var realtimeService = new PendingRealtimeService();
        var component = new TestManageTeams();

        SetProperty(component, "TeamService", teamService);
        SetProperty(component, "NotificationService", new NoopNotificationService());
        SetProperty(component, "RealtimeService", realtimeService);

        component.StartInitialization();
        await realtimeService.StartAttempted.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(component.FirstRender.IsCompleted);
        Assert.False(component.IsLoading);
        Assert.Equal(teamId, Assert.Single(component.CurrentSummary.CaptainedTeams).Id);
        Assert.Equal(1, Volatile.Read(ref summaryRequests));
        Assert.Equal(1, realtimeService.StartCalls);
        Assert.False(realtimeService.StartTask.IsCompleted);

        await component.DisposeAsync();
    }

    [Fact]
    public async Task NavMenuDoesNotContinueAuthenticatedInitializationAfterDisposal()
    {
        var profileRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var profile = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var userClient = CreateProxy<IUserClient>((method, _) =>
        {
            if(method.Name != nameof(IUserClient.GetCurrentUserProfileAsync))
                throw new NotSupportedException(method.Name);

            profileRequested.SetResult(true);
            return profile.Task;
        });
        var notificationService = new TrackingNotificationService();
        var realtimeService = new TrackingRealtimeService();
        var component = new TestNavMenu();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            authenticationType: "test"));

        SetProperty(component, "UserClient", userClient);
        SetProperty(component, "NotificationService", notificationService);
        SetProperty(component, "TeamRealtimeService", realtimeService);
        SetProperty(component, "AuthenticationStateTask", Task.FromResult(new AuthenticationState(principal)));

        component.StartInitialization();
        await component.ApplyParametersAsync();
        Assert.False(profileRequested.Task.IsCompleted);
        await component.StartInteractiveRenderAsync();
        await profileRequested.Task;
        await component.DisposeAsync();

        profile.SetResult(new CurrentUserProfileResponse(true, null, null, true));
        await Task.Delay(25);

        Assert.Equal(0, notificationService.RefreshCalls);
        Assert.Equal(0, realtimeService.StartCalls);
    }

    [Fact]
    public async Task ProfileRedirectDefersApiCheckUntilInteractiveRenderAndIgnoresDisposedWork()
    {
        var profile = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var profileCalls = 0;
        var userClient = CreateProxy<IUserClient>((method, _) =>
        {
            if(method.Name != nameof(IUserClient.GetCurrentUserProfileAsync))
                throw new NotSupportedException(method.Name);

            Interlocked.Increment(ref profileCalls);
            return profile.Task;
        });
        var navigationManager = new RecordingNavigationManager("https://example.test/profile");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            authenticationType: "test"));
        var component = new TestProfileRedirect();

        SetProperty(component, "UserClient", userClient);
        SetProperty(component, "NavigationManager", navigationManager);
        SetProperty(component, "AuthenticationStateProvider", new FixedAuthenticationStateProvider(principal));

        component.ApplyParameters();
        Assert.Equal(0, profileCalls);

        await component.StartInteractiveRenderAsync();
        await WaitForAsync(() => Volatile.Read(ref profileCalls) == 1);

        await component.DisposeAsync();
        profile.SetResult(new CurrentUserProfileResponse(false, null, null, true));
        await Task.Delay(25);

        Assert.Empty(navigationManager.Navigations);
    }

    [Fact]
    public async Task LiveLogoutCompletesLocallyWhenRemoteSignOutFails()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                options.Cookie.Name = "logout-pipeline-auth")
            .AddScheme<AuthenticationSchemeOptions, FailingRemoteSignOutHandler>(
                Auth0Constants.AuthenticationScheme,
                _ => { })
            .Services
            .BuildServiceProvider();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("LogoutPipelineTest");
        var applicationBuilder = new ApplicationBuilder(services);
        applicationBuilder.UseAuthentication();
        applicationBuilder.Run(async context =>
        {
            if(context.Request.Path == "/account/logout")
            {
                await AccountLogoutFlow.BeginLiveAsync(context, "/tournaments?view=upcoming#schedule", logger);
                return;
            }

            context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true
                ? StatusCodes.Status200OK
                : StatusCodes.Status401Unauthorized;
        });
        var pipeline = applicationBuilder.Build();
        var origin = new Uri("https://example.test/");
        var cookieJar = new CookieContainer();

        await using(var signInScope = services.CreateAsyncScope())
        {
            var signInContext = CreatePipelineContext(signInScope.ServiceProvider, "/account/login");
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "logout-user")], CookieAuthenticationDefaults.AuthenticationScheme);
            await signInContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            await signInContext.Response.CompleteAsync();
            ApplyResponseCookies(cookieJar, origin, signInContext.Response.Headers.SetCookie);
        }

        Assert.Contains("logout-pipeline-auth=", cookieJar.GetCookieHeader(origin), StringComparison.Ordinal);

        await using(var logoutScope = services.CreateAsyncScope())
        {
            var logoutContext = CreatePipelineContext(logoutScope.ServiceProvider, "/account/logout", cookieJar.GetCookieHeader(origin));
            await pipeline(logoutContext);
            await logoutContext.Response.CompleteAsync();

            Assert.Equal(StatusCodes.Status302Found, logoutContext.Response.StatusCode);
            Assert.Equal("/tournaments?view=upcoming#schedule", logoutContext.Response.Headers.Location);
            Assert.Contains("logout-pipeline-auth=", logoutContext.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
            ApplyResponseCookies(cookieJar, origin, logoutContext.Response.Headers.SetCookie);
        }

        await using(var protectedScope = services.CreateAsyncScope())
        {
            var protectedContext = CreatePipelineContext(protectedScope.ServiceProvider, "/teams/manage", cookieJar.GetCookieHeader(origin));
            await pipeline(protectedContext);
            await protectedContext.Response.CompleteAsync();

            Assert.Equal(StatusCodes.Status401Unauthorized, protectedContext.Response.StatusCode);
            Assert.NotEqual(true, protectedContext.User.Identity?.IsAuthenticated);
        }
    }

    private static DefaultHttpContext CreatePipelineContext(IServiceProvider services, string path, string? cookieHeader = null)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if(!string.IsNullOrEmpty(cookieHeader))
            context.Request.Headers.Cookie = cookieHeader;

        return context;
    }

    private static void ApplyResponseCookies(CookieContainer cookieJar, Uri origin, IEnumerable<string?> setCookieHeaders)
    {
        foreach(var setCookieHeader in setCookieHeaders)
        {
            if(!string.IsNullOrEmpty(setCookieHeader))
                cookieJar.SetCookies(origin, setCookieHeader);
        }
    }

    private static T CreateProxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, ServiceProxy>();
        ((ServiceProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? instance.GetType().BaseType?.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);

        property.SetValue(instance, value);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while(!condition())
        {
            if(DateTime.UtcNow >= timeout)
                throw new TimeoutException("The expected lifecycle event did not occur.");

            await Task.Delay(10);
        }
    }

    private class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = (_, _) => null;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Missing proxy method."), args);
    }

    private sealed class TestHome : Home
    {
        public int RenderCount => Volatile.Read(ref _renderCount);

        private int _renderCount;

        public Task StartInitialRenderAsync() => base.OnAfterRenderAsync(firstRender: true);

        protected override Task RequestRenderAsync()
        {
            Interlocked.Increment(ref _renderCount);
            return Task.CompletedTask;
        }
    }

    private sealed class TestTournamentsOverview : TournamentsOverview
    {
        public int RenderCount => Volatile.Read(ref _renderCount);

        private int _renderCount;

        public Task StartInitialRenderAsync() => base.OnAfterRenderAsync(firstRender: true);

        protected override Task RequestRenderAsync()
        {
            Interlocked.Increment(ref _renderCount);
            return Task.CompletedTask;
        }
    }

    private sealed class TestManageTeams : ManageTeams
    {
        private readonly TaskCompletionSource _firstRender = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstRender => _firstRender.Task;

        public bool IsLoading => (bool)(typeof(ManageTeams)
            .GetField("_isLoading", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(this) ?? throw new MissingFieldException(typeof(ManageTeams).FullName, "_isLoading"));

        public CurrentUserTeamSummaryDTO CurrentSummary => (CurrentUserTeamSummaryDTO)(typeof(ManageTeams)
            .GetField("_summary", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(this) ?? throw new MissingFieldException(typeof(ManageTeams).FullName, "_summary"));

        public void StartInitialization() => base.OnInitialized();

        protected override Task RequestRenderAsync()
        {
            _firstRender.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class TestNavMenu : NavMenu
    {
        public void StartInitialization() => base.OnInitialized();

        public Task ApplyParametersAsync() => base.OnParametersSetAsync();

        public Task StartInteractiveRenderAsync() => base.OnAfterRenderAsync(firstRender: true);
    }

    private sealed class TestProfileRedirect : ProfileRedirect
    {
        public void ApplyParameters() => base.OnParametersSet();

        public Task StartInteractiveRenderAsync() => base.OnAfterRenderAsync(firstRender: true);
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public List<string> Navigations { get; } = [];

        public RecordingNavigationManager(string uri)
        {
            Initialize("https://example.test/", uri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options) => Navigations.Add(uri);
    }

    private class NoopNotificationService : ITeamNotificationService
    {
        public event Func<Task>? Changed;
        public event Func<Guid, Task>? RosterDecisionChanged;
        public IReadOnlyList<TeamNotificationItem> Notifications => Array.Empty<TeamNotificationItem>();
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

    private sealed class PendingRealtimeService : ITeamRealtimeService
    {
        private readonly TaskCompletionSource _start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _startAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Func<Task>? TeamStateInvalidated;
        public bool IsConnected => false;
        public int StartCalls { get; private set; }
        public Task StartTask => _start.Task;
        public Task StartAttempted => _startAttempted.Task;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            _startAttempted.TrySetResult();
            return _start.Task.WaitAsync(cancellationToken);
        }

        public Task JoinTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingRemoteSignOutHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignOutHandler
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task SignOutAsync(AuthenticationProperties? properties) =>
            throw new InvalidOperationException("Auth0 unavailable");
    }
}
