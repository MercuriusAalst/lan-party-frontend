using System.Reflection;
using System.Security.Claims;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentParticipantsLoadingTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public async Task FailedPopupLoadIsRetriedAfterCloseAndReopen()
    {
        var service = CreateTournamentService();
        service.FailFirstStateRequest = true;
        var tab = CreateTab(service, popupOnly: true, dialogOpen: true);

        await tab.LoadAfterRenderAsync();
        Assert.Equal(1, service.StateRequestCount);
        Assert.False(GetField<bool>(tab, "_registrationLoadCompleted"));

        tab.SetDialogOpenForTest(false);
        tab.SetDialogOpenForTest(true);
        await tab.LoadAfterRenderAsync();

        Assert.Equal(2, service.StateRequestCount);
        Assert.True(GetField<bool>(tab, "_registrationLoadCompleted"));
    }

    [Fact]
    public async Task PopupOnlyLoadDoesNotRequestAdminRegistrations()
    {
        var service = CreateTournamentService();
        var tab = CreateTab(service, popupOnly: true, dialogOpen: true, isAdmin: true);

        await tab.LoadAfterRenderAsync();

        Assert.Equal(0, service.AdminRegistrationRequestCount);
    }

    [Fact]
    public void RegistrationDialogHidesWorkflowUntilContextLoadFinishes()
    {
        var service = CreateTournamentService();
        var tab = CreateTab(service, popupOnly: true, dialogOpen: true);
        var tournament = CreateTournament();
        tournament.ParticipationMode = ParticipationMode.Team;
        tournament.TeamSize = 2;
        tab.SetTournamentForTest(tournament);

        var initialContent = tab.RenderMarkupText();
        Assert.Contains("Feature.tournaments.checkingYourOptions", initialContent);
        Assert.DoesNotContain("Feature.tournaments.signInRegistration", initialContent);
        Assert.DoesNotContain("Feature.tournaments.noCaptainedTeamTitle", initialContent);
        Assert.DoesNotContain("Feature.tournaments.createTeam", initialContent);

        SetPrivateField(tab, "_registrationLoadRequested", false);
        var signedOutContent = tab.RenderMarkupText();
        Assert.Contains("Feature.tournaments.signInRegistration", signedOutContent);
        Assert.DoesNotContain("Feature.tournaments.checkingYourOptions", signedOutContent);
        Assert.DoesNotContain("Feature.tournaments.noCaptainedTeamTitle", signedOutContent);
        Assert.DoesNotContain("Feature.tournaments.createTeam", signedOutContent);

        SetPrivateField(tab, "_isLoadingRegistration", true);
        SetPrivateField(tab, "_isAuthenticated", true);
        var loadingContent = tab.RenderMarkupText();
        Assert.Contains("Feature.tournaments.checkingYourOptions", loadingContent);
        Assert.DoesNotContain("Feature.tournaments.signInRegistration", loadingContent);
        Assert.DoesNotContain("Feature.tournaments.noCaptainedTeamTitle", loadingContent);
        Assert.DoesNotContain("Feature.tournaments.createTeam", loadingContent);
        Assert.DoesNotContain("Feature.tournaments.selectTeam", loadingContent);

        SetPrivateField(tab, "_isLoadingRegistration", false);
        SetPrivateField(tab, "_teamSummary", new CurrentUserTeamSummaryDTO());
        var resolvedContent = tab.RenderMarkupText();
        Assert.Contains("Feature.tournaments.noCaptainedTeamTitle", resolvedContent);
        Assert.Contains("Feature.tournaments.createTeam", resolvedContent);
        Assert.DoesNotContain("Feature.tournaments.noTeamAvailable", resolvedContent);
        Assert.DoesNotContain("Feature.tournaments.checkingYourOptions", resolvedContent);
        Assert.DoesNotContain("Feature.tournaments.signInRegistration", resolvedContent);

        SetPrivateField(tab, "_registrationError", "Registration context failed.");
        var failedContent = tab.RenderMarkupText();
        Assert.Contains("Registration context failed.", failedContent);
        Assert.DoesNotContain("Feature.tournaments.checkingYourOptions", failedContent);

        SetPrivateField(tab, "_teamSummaryUnavailable", true);
        var teamLoadFailedContent = tab.RenderMarkupText();
        Assert.Contains("Feature.tournaments.teamsUnavailable", teamLoadFailedContent);
        Assert.DoesNotContain("Feature.tournaments.noCaptainedTeamTitle", teamLoadFailedContent);
        Assert.DoesNotContain("Feature.tournaments.createTeam", teamLoadFailedContent);
    }

    [Fact]
    public async Task ClosingRegistrationPopupCancelsDelayedLoad()
    {
        var service = CreateTournamentService();
        service.DelayStateRequest = true;
        var tab = CreateTab(service, popupOnly: true, dialogOpen: true);

        var load = tab.LoadAfterRenderAsync();
        await service.StateRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await tab.CloseRegistrationForTestAsync();
        await load;

        Assert.True(service.StateCancellationObserved);
        Assert.False(GetField<bool>(tab, "_isRegistrationDialogOpen"));
    }

    [Fact]
    public async Task DisposingRegistrationPopupCancelsDelayedLoad()
    {
        var service = CreateTournamentService();
        service.DelayStateRequest = true;
        var tab = CreateTab(service, popupOnly: true, dialogOpen: true);

        var load = tab.LoadAfterRenderAsync();
        await service.StateRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await tab.DisposeAsync();
        await load;

        Assert.True(service.StateCancellationObserved);
    }

    [Fact]
    public async Task NewTournamentLoadCancelsPreviousLoadBeforeRetrying()
    {
        var service = CreateTournamentService();
        service.DelayStateRequest = true;
        service.HoldCanceledStateRequest = true;
        var tab = CreateTab(service, popupOnly: true, dialogOpen: true);

        var firstLoad = tab.LoadAfterRenderAsync();
        await service.StateRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        service.DelayStateRequest = false;
        tab.SetTournamentForTest(CreateTournament(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
        var replacementLoad = tab.LoadAfterRenderAsync();

        await Task.WhenAll(
            service.CanceledStateRequestObserved.Task.WaitAsync(TimeSpan.FromSeconds(2)),
            service.SecondStateRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.False(service.CanceledStateRequestRelease.Task.IsCompleted);
        service.CanceledStateRequestRelease.TrySetResult(true);
        await Task.WhenAll(firstLoad, replacementLoad);

        Assert.True(service.StateCancellationObserved);
        Assert.Equal(2, service.StateRequestCount);
        Assert.True(GetField<bool>(tab, "_registrationLoadCompleted"));
    }

    [Fact]
    public async Task BellRosterDecisionRefreshesOpenRegistrationWithoutRealtime()
    {
        var service = CreateTournamentService();
        var notifications = new RecordingNotificationService();
        var tab = CreateTab(
            service,
            popupOnly: false,
            dialogOpen: false,
            notificationService: notifications);
        SetPrivateField(tab, "_isAuthenticated", true);
        SetPrivateField(tab, "_registrationState", new CurrentUserTournamentRegistrationStateDTO
        {
            TournamentId = tab.Tournament.Id,
            PendingRosterConfirmation = new TournamentRosterMemberDTO
            {
                Id = Guid.NewGuid(),
                User = new PublicUserDTO { Id = Guid.NewGuid(), Username = "selected" },
                ConfirmationStatus = RosterMemberConfirmationStatus.Pending
            },
            CanConfirmRoster = true
        });
        tab.InitializeForTest();

        await notifications.RaiseRosterDecisionChangedAsync(tab.Tournament.Id);

        Assert.Equal(1, service.StateRequestCount);
        var refreshedState = GetField<CurrentUserTournamentRegistrationStateDTO?>(tab, "_registrationState");
        Assert.NotNull(refreshedState);
        Assert.Null(refreshedState!.PendingRosterConfirmation);

        await tab.DisposeAsync();
        await notifications.RaiseRosterDecisionChangedAsync(tab.Tournament.Id);

        Assert.Equal(1, service.StateRequestCount);
    }

    private static TestableTournamentParticipantsTab CreateTab(
        RecordingTournamentServiceProxy service,
        bool popupOnly,
        bool dialogOpen,
        bool isAdmin = false,
        ITeamNotificationService? notificationService = null)
    {
        var tab = new TestableTournamentParticipantsTab();
        SetPrivateProperty(tab, "Localization", TestLocalizationService.Instance);
        tab.Tournament = CreateTournament();
        tab.PopupOnly = popupOnly;
        tab.RegistrationDialogOpen = dialogOpen;
        SetPrivateProperty(tab, "TournamentService", service.Proxy);
        SetPrivateProperty(tab, "AuthenticationStateProvider", new FixedAuthenticationStateProvider(CreatePrincipal(isAdmin)));
        SetPrivateProperty(tab, "TeamRealtimeService", new NoopTeamRealtimeService());
        SetPrivateProperty(tab, "NotificationService", notificationService ?? new RecordingNotificationService());
        SetPrivateField(tab, "_registrationFocusTrap", DispatchProxy.Create<IJSObjectReference, NoopJsObjectReferenceProxy>());
        tab.SetParametersForTest();
        return tab;
    }

    private static ClaimsPrincipal CreatePrincipal(bool isAdmin)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "test-user") };
        if(isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "admin"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static TournamentExtended CreateTournament(Guid? id = null) => new()
    {
        Id = id ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Name = "Test tournament",
        Status = TournamentStatus.Scheduled,
        ParticipationMode = ParticipationMode.Individual
    };

    private static RecordingTournamentServiceProxy CreateTournamentService()
    {
        var proxy = DispatchProxy.Create<ITournamentService, RecordingTournamentServiceProxy>();
        var recording = (RecordingTournamentServiceProxy)(object)proxy;
        recording.Proxy = proxy;
        return recording;
    }

    private static T GetField<T>(object instance, string name) =>
        (T)typeof(TournamentParticipantsTab).GetField(name, PrivateInstance)!.GetValue(instance)!;

    private static void SetPrivateField(object instance, string name, object? value) =>
        typeof(TournamentParticipantsTab).GetField(name, PrivateInstance)!.SetValue(instance, value);

    private static void SetPrivateProperty(object instance, string name, object value) =>
        typeof(TournamentParticipantsTab).GetProperty(name, PrivateInstance)!.SetValue(instance, value);

    private sealed class TestableTournamentParticipantsTab : TournamentParticipantsTab
    {
        private readonly TestRenderer _renderer = new();

        public TestableTournamentParticipantsTab()
        {
            _renderer.Attach(this);
        }

        public void SetParametersForTest() => base.OnParametersSet();

        public void SetTournamentForTest(TournamentExtended tournament)
        {
            Tournament = tournament;
            base.OnParametersSet();
        }

        public Task LoadAfterRenderAsync() => base.OnAfterRenderAsync(false);

        public string RenderMarkupText()
        {
            var builder = new RenderTreeBuilder();
            typeof(TournamentParticipantsTab)
                .GetMethod("BuildRenderTree", PrivateInstance)!
                .Invoke(this, [builder]);
            var frames = builder.GetFrames();
            return string.Concat(frames.Array.Take(frames.Count)
                .Where(frame => frame.FrameType == RenderTreeFrameType.Text)
                .Select(frame => frame.TextContent));
        }

        public void InitializeForTest() => base.OnInitialized();

        public void SetDialogOpenForTest(bool isOpen)
        {
            RegistrationDialogOpen = isOpen;
            base.OnParametersSet();
        }

        public Task CloseRegistrationForTestAsync()
        {
            var method = typeof(TournamentParticipantsTab).GetMethod(
                "CloseRegistrationDialogAsync",
                PrivateInstance)!;
            return (Task)method.Invoke(this, null)!;
        }
    }

    private sealed class TestRenderer : Renderer
    {
        public TestRenderer()
            : base(new ServiceCollection()
                .AddSingleton<IJSRuntime, NoopJsRuntime>()
                .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
                .BuildServiceProvider(), NullLoggerFactory.Instance)
        {
        }

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public void Attach(IComponent component) => AssignRootComponentId(component);

        protected override void HandleException(Exception exception) => throw exception;

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            new(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            new(default(TValue)!);
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(principal);

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(_state);
    }

    private class NoopJsObjectReferenceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.ReturnType == typeof(ValueTask))
                return new ValueTask();

            if(targetMethod?.ReturnType.IsGenericType == true &&
               targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                return Activator.CreateInstance(targetMethod.ReturnType);

            return null;
        }
    }

    private class RecordingTournamentServiceProxy : DispatchProxy
    {
        public ITournamentService Proxy { get; set; } = null!;
        public bool FailFirstStateRequest { get; set; }
        public bool DelayStateRequest { get; set; }
        public bool HoldCanceledStateRequest { get; set; }
        public int StateRequestCount { get; private set; }
        public int AdminRegistrationRequestCount { get; private set; }
        public bool StateCancellationObserved { get; private set; }
        public TaskCompletionSource<bool> StateRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SecondStateRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CanceledStateRequestObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CanceledStateRequestRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITournamentService.GetCurrentUserTournamentRegistrationStateAsync) => GetState((CancellationToken)args![1]!),
                nameof(ITournamentService.CheckIndividualTournamentRegistrationEligibilityAsync) =>
                    Task.FromResult(new EligibilityResponseDTO { Eligible = true }),
                nameof(ITournamentService.GetAdminTournamentRegistrationsAsync) => GetAdminRegistrations(),
                _ => throw new NotSupportedException($"Unexpected tournament service call: {targetMethod?.Name}")
            };
        }

        private Task<CurrentUserTournamentRegistrationStateDTO> GetState(CancellationToken cancellationToken)
        {
            StateRequestCount++;
            StateRequestStarted.TrySetResult(true);
            if(StateRequestCount == 2)
                SecondStateRequestStarted.TrySetResult(true);
            if(DelayStateRequest)
            {
                return WaitForCancellationAsync(cancellationToken);
            }

            if(FailFirstStateRequest && StateRequestCount == 1)
                throw new InvalidOperationException("temporary failure");

            return Task.FromResult(new CurrentUserTournamentRegistrationStateDTO
            {
                TournamentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CanRegisterIndividual = true
            });
        }

        private Task<List<AdminTournamentRegistrationDTO>> GetAdminRegistrations()
        {
            AdminRegistrationRequestCount++;
            return Task.FromResult(new List<AdminTournamentRegistrationDTO>());
        }

        private async Task<CurrentUserTournamentRegistrationStateDTO> WaitForCancellationAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch(OperationCanceledException)
            {
                StateCancellationObserved = true;
                CanceledStateRequestObserved.TrySetResult(true);
                if(HoldCanceledStateRequest)
                    await CanceledStateRequestRelease.Task;
                throw;
            }

            throw new InvalidOperationException("The delayed test request should only complete through cancellation.");
        }
    }

    private sealed class RecordingNotificationService : ITeamNotificationService
    {
        public event Func<Task>? Changed;
        public event Func<Guid, Task>? RosterDecisionChanged;
        public IReadOnlyList<TeamNotificationItem> Notifications => [];
        public int UnreadCount => 0;

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RespondToRosterSelectionAsync(
            string notificationId,
            bool accept,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MarkAllReadAsync() => Task.CompletedTask;

        public Task DismissAsync(string id) => Task.CompletedTask;

        public async Task RaiseRosterDecisionChangedAsync(Guid tournamentId)
        {
            var handler = RosterDecisionChanged;
            if(handler is not null)
                await handler(tournamentId);
        }
    }
}
