using System.Reflection;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentParticipantsMutationTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public async Task UserMutationStaleSuccessAfterTournamentChangeHasNoEffects()
    {
        var tournamentAId = Guid.NewGuid();
        var tournamentBId = Guid.NewGuid();
        var tournamentBRegistration = CreatePublicRegistration(Guid.NewGuid(), tournamentBId);
        var tournamentA = CreateTournament(tournamentAId);
        var tournamentB = CreateTournament(tournamentBId, tournamentBRegistration);
        var completion = NewCompletionSource<TournamentRegistrationDTO?>();
        var tab = CreateTab(out var toastService);
        var callbackCount = 0;
        SetUpdatedCallback(tab, _ => callbackCount++);

        tab.SetTournamentForTest(tournamentA);
        var mutation = InvokeUserMutation(tab, () => completion.Task);

        Assert.True(GetField<bool>(tab, "_isSubmitting"));
        tab.SetTournamentForTest(tournamentB);
        completion.SetResult(CreateRegistration(Guid.NewGuid(), tournamentAId));

        await mutation;

        Assert.Equal([tournamentBRegistration.Id], tournamentB.Registrations.Select(registration => registration.Id));
        Assert.Null(GetField(tab, "_registrationError"));
        Assert.False(GetField<bool>(tab, "_isSubmitting"));
        Assert.Empty(toastService.Messages);
        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public async Task UserMutationStaleErrorAfterTournamentChangeHasNoEffects()
    {
        var tournamentA = CreateTournament(Guid.NewGuid());
        var tournamentB = CreateTournament(Guid.NewGuid());
        var completion = NewCompletionSource<TournamentRegistrationDTO?>();
        var tab = CreateTab(out var toastService);
        var callbackCount = 0;
        SetUpdatedCallback(tab, _ => callbackCount++);

        tab.SetTournamentForTest(tournamentA);
        var mutation = InvokeUserMutation(tab, () => completion.Task);

        tab.SetTournamentForTest(tournamentB);
        completion.SetException(new InvalidOperationException("Tournament A failed."));

        await mutation;

        Assert.Null(GetField(tab, "_registrationError"));
        Assert.False(GetField<bool>(tab, "_isSubmitting"));
        Assert.Empty(toastService.Messages);
        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public async Task AdminMutationStaleSuccessAfterTournamentChangeHasNoEffects()
    {
        var tournamentAId = Guid.NewGuid();
        var tournamentBId = Guid.NewGuid();
        var tournamentBRegistration = CreatePublicRegistration(Guid.NewGuid(), tournamentBId);
        var tournamentA = CreateTournament(tournamentAId);
        var tournamentB = CreateTournament(tournamentBId, tournamentBRegistration);
        var completion = NewCompletionSource<bool>();
        var tab = CreateTab(out var toastService);
        var service = CreateTournamentService(completion);
        SetPrivateProperty(tab, "TournamentService", service);
        SetPrivateField(tab, "_adminRemovalReason", "Tournament A reason.");
        var callbackCount = 0;
        SetUpdatedCallback(tab, _ => callbackCount++);
        var registration = CreateAdminRegistration(Guid.NewGuid(), tournamentAId);

        tab.SetTournamentForTest(tournamentA);
        var mutation = InvokeAdminMutation(tab, registration);

        await service.CallStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(registration.Id, GetField<Guid?>(tab, "_pendingAdminRemovalRegistrationId"));
        tab.SetTournamentForTest(tournamentB);
        SetPrivateField(tab, "_adminRemovalReason", "Tournament B reason.");
        completion.SetResult(true);

        await mutation;

        Assert.Equal([tournamentBRegistration.Id], tournamentB.Registrations.Select(item => item.Id));
        Assert.Equal("Tournament B reason.", GetField<string>(tab, "_adminRemovalReason"));
        Assert.Null(GetField(tab, "_adminError"));
        Assert.Null(GetField(tab, "_pendingAdminRemovalRegistrationId"));
        Assert.Empty(toastService.Messages);
        Assert.Equal(0, callbackCount);
        var call = Assert.Single(service.Calls);
        Assert.Equal(tournamentAId, call.TournamentId);
    }

    [Fact]
    public async Task AdminMutationStaleErrorAfterTournamentChangeHasNoEffects()
    {
        var tournamentA = CreateTournament(Guid.NewGuid());
        var tournamentB = CreateTournament(Guid.NewGuid());
        var completion = NewCompletionSource<bool>();
        var tab = CreateTab(out var toastService);
        var service = CreateTournamentService(completion);
        SetPrivateProperty(tab, "TournamentService", service);
        SetPrivateField(tab, "_adminRemovalReason", "Tournament A reason.");
        var callbackCount = 0;
        SetUpdatedCallback(tab, _ => callbackCount++);
        var registration = CreateAdminRegistration(Guid.NewGuid(), tournamentA.Id);

        tab.SetTournamentForTest(tournamentA);
        var mutation = InvokeAdminMutation(tab, registration);

        tab.SetTournamentForTest(tournamentB);
        SetPrivateField(tab, "_adminRemovalReason", "Tournament B reason.");
        completion.SetException(new InvalidOperationException("Tournament A failed."));

        await mutation;

        Assert.Equal("Tournament B reason.", GetField<string>(tab, "_adminRemovalReason"));
        Assert.Null(GetField(tab, "_adminError"));
        Assert.Null(GetField(tab, "_pendingAdminRemovalRegistrationId"));
        Assert.Empty(toastService.Messages);
        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public async Task IndividualRegistrationActionOpensTheInlineConfirmationWithoutSendingTheMutation()
    {
        var tournament = CreateTournament(Guid.NewGuid());
        var tab = CreateTab(out _);
        var service = CreateIndividualRegistrationService();
        SetPrivateProperty(tab, "TournamentService", service);
        tab.SetTournamentForTest(tournament);
        SeedIndividualState(tab, service);

        await InvokePrivateAsync(tab, "OpenRegistrationDialog");

        Assert.True(GetField<bool>(tab, "_isIndividualRegistrationConfirmationOpen"));
        Assert.False(GetField<bool>(tab, "_isRegistrationDialogOpen"));
        Assert.Equal(0, service.RegisterCallCount);
        Assert.Empty(service.Calls);
    }

    [Fact]
    public async Task ConfirmingIndividualRegistrationSendsTheMutationExactlyOnce()
    {
        var tournament = CreateTournament(Guid.NewGuid());
        var tab = CreateTab(out _);
        var service = CreateIndividualRegistrationService();
        SetPrivateProperty(tab, "TournamentService", service);
        tab.SetTournamentForTest(tournament);
        SeedIndividualState(tab, service);

        await InvokePrivateAsync(tab, "OpenRegistrationDialog");
        await InvokePrivateAsync(tab, "RegisterIndividualAsync");

        Assert.Equal(1, service.RegisterCallCount);
        Assert.Equal([tournament.Id], service.RegisterTournamentIds);
        Assert.False(GetField<bool>(tab, "_isIndividualRegistrationConfirmationOpen"));
    }

    [Fact]
    public async Task DismissingIndividualRegistrationConfirmationSendsNoMutation()
    {
        var tournament = CreateTournament(Guid.NewGuid());
        var tab = CreateTab(out _);
        var service = CreateIndividualRegistrationService();
        SetPrivateProperty(tab, "TournamentService", service);
        tab.SetTournamentForTest(tournament);
        SeedIndividualState(tab, service);

        await InvokePrivateAsync(tab, "OpenRegistrationDialog");
        await InvokePrivateAsync(tab, "DismissIndividualRegistrationConfirmationAsync");

        Assert.False(GetField<bool>(tab, "_isIndividualRegistrationConfirmationOpen"));
        Assert.Equal(0, service.RegisterCallCount);
        Assert.Empty(service.Calls);
    }

    [Fact]
    public async Task IneligibleIndividualPlayerStillUsesTheRegistrationDialog()
    {
        var tournament = CreateTournament(Guid.NewGuid());
        var tab = CreateTab(out _);
        var service = CreateIndividualRegistrationService();
        SetPrivateProperty(tab, "TournamentService", service);
        tab.SetTournamentForTest(tournament);
        SeedIndividualState(tab, service, eligible: false);

        await InvokePrivateAsync(tab, "OpenRegistrationDialog");

        Assert.True(GetField<bool>(tab, "_isRegistrationDialogOpen"));
        Assert.False(GetField<bool>(tab, "_isIndividualRegistrationConfirmationOpen"));
        Assert.Empty(service.Calls);
    }

    [Fact]
    public async Task IndividualUnregistrationRequiresTheConfirmationBeforeTheMutation()
    {
        var tournament = CreateTournament(Guid.NewGuid());
        var tab = CreateTab(out _);
        var service = CreateIndividualRegistrationService();
        SetPrivateProperty(tab, "TournamentService", service);
        tab.SetTournamentForTest(tournament);
        SeedIndividualState(tab, service, registered: true);

        await InvokePrivateAsync(tab, "BeginIndividualUnregistrationConfirmationAsync");

        Assert.True(GetField<bool>(tab, "_isIndividualUnregistrationConfirmationOpen"));
        Assert.Equal(0, service.DeleteCallCount);

        await InvokePrivateAsync(tab, "UnregisterIndividualAsync");

        Assert.Equal(1, service.DeleteCallCount);
        Assert.Equal([tournament.Id], service.DeleteTournamentIds);
        Assert.False(GetField<bool>(tab, "_isIndividualUnregistrationConfirmationOpen"));
    }

    [Fact]
    public async Task RosterRevalidationKeepsTheStepStateAndExplainsABlockedStep()
    {
        var teamId = Guid.NewGuid();
        var captainId = Guid.NewGuid();
        var tab = CreateTab(out _);
        var service = CreateRosterEligibilityService();
        SetPrivateProperty(tab, "TournamentService", service);
        tab.SetTournamentForTest(CreateTeamTournament(Guid.NewGuid()));
        SeedSelectedRegistrableTeam(tab, teamId, captainId);

        var previousEligibility = new RosterCandidateEligibilityResponseDTO { Eligible = true };
        SetPrivateField(tab, "_rosterEligibility", previousEligibility);
        SetPrivateField(tab, "_selectedRosterUserIds", new HashSet<Guid> { captainId });

        bool? loadingDuringCheck = null;
        object? eligibilityDuringCheck = null;
        service.OnCheck = () =>
        {
            loadingDuringCheck = GetField<bool>(tab, "_isLoadingRoster");
            eligibilityDuringCheck = GetField(tab, "_rosterEligibility");
        };
        service.Eligibility = new RosterCandidateEligibilityResponseDTO
        {
            Eligible = false,
            ReasonCodes = ["roster-member-ineligible"]
        };

        var method = typeof(TournamentParticipantsTab).GetMethod("RevalidateRosterBeforeSubmitAsync", PrivateInstance)!;
        var request = new SubmitTeamRosterDTO { TeamId = teamId, UserIds = [captainId] };
        var testTab = (TestableTournamentParticipantsTab)tab;
        var requestGeneration = GetField<long>(tab, "_requestGeneration");

        var revalidated = await testTab.InvokeOnRendererAsync(
            () => (Task<bool>)method.Invoke(tab, [teamId, request, requestGeneration])!);

        Assert.False(revalidated);
        Assert.False(loadingDuringCheck);
        Assert.Same(previousEligibility, eligibilityDuringCheck);
        Assert.Equal("Feature.tournaments.rosterMemberIneligible", GetField<string>(tab, "_teamError"));
        Assert.Single(service.Calls);
    }

    private static TestableTournamentParticipantsTab CreateTab(out RecordingToastServiceProxy toastService)
    {
        var tab = new TestableTournamentParticipantsTab();
        SetPrivateProperty(tab, "Localization", TestLocalizationService.Instance);
        var toast = DispatchProxy.Create<IToastService, RecordingToastServiceProxy>();
        toastService = (RecordingToastServiceProxy)(object)toast;
        SetPrivateProperty(tab, "ToastService", toast);
        return tab;
    }

    private static RecordingTournamentServiceProxy CreateTournamentService(TaskCompletionSource<bool> completion)
    {
        var service = DispatchProxy.Create<ITournamentService, RecordingTournamentServiceProxy>();
        var proxy = (RecordingTournamentServiceProxy)(object)service;
        proxy.RemovalCompletion = completion;
        return proxy;
    }

    private static RecordingIndividualRegistrationServiceProxy CreateIndividualRegistrationService() =>
        (RecordingIndividualRegistrationServiceProxy)(object)
        DispatchProxy.Create<ITournamentService, RecordingIndividualRegistrationServiceProxy>();

    private static RosterEligibilityServiceProxy CreateRosterEligibilityService() =>
        (RosterEligibilityServiceProxy)(object)
        DispatchProxy.Create<ITournamentService, RosterEligibilityServiceProxy>();

    private static TournamentExtended CreateTeamTournament(Guid id) => new()
    {
        Id = id,
        Name = id.ToString(),
        Status = TournamentStatus.Scheduled,
        ParticipationMode = ParticipationMode.Team,
        TeamSize = 2
    };

    private static void SeedSelectedRegistrableTeam(
        TournamentParticipantsTab tab,
        Guid teamId,
        Guid captainId)
    {
        SetPrivateField(
            tab,
            "_teamSummary",
            new CurrentUserTeamSummaryDTO
            {
                CaptainedTeams =
                [
                    new TeamManagementSummaryDTO
                    {
                        Id = teamId,
                        Name = "Alpha",
                        CaptainUserId = captainId,
                        Members =
                        [
                            CreateUser(captainId, "captain"),
                            CreateUser(Guid.NewGuid(), "teammate")
                        ]
                    }
                ]
            });
        SetPrivateField(tab, "_selectedTeamId", teamId);
    }

    private static void SeedIndividualState(
        TournamentParticipantsTab tab,
        RecordingIndividualRegistrationServiceProxy service,
        bool eligible = true,
        bool registered = false)
    {
        var state = new CurrentUserTournamentRegistrationStateDTO
        {
            TournamentId = tab.Tournament.Id,
            CanRegisterIndividual = !registered,
            CanUnregister = registered,
            IndividualRegistration = registered
                ? CreateRegistration(Guid.NewGuid(), tab.Tournament.Id)
                : null
        };
        service.State = state;
        service.Eligibility = new EligibilityResponseDTO { Eligible = eligible };
        SetPrivateField(tab, "_isAuthenticated", true);
        SetPrivateField(tab, "_registrationState", state);
        SetPrivateField(tab, "_individualEligibility", service.Eligibility);
    }

    private static Task InvokePrivateAsync(TournamentParticipantsTab tab, string methodName)
    {
        var method = typeof(TournamentParticipantsTab).GetMethod(methodName, PrivateInstance)!;
        var testTab = (TestableTournamentParticipantsTab)tab;
        return testTab.InvokeOnRendererAsync(() => (Task)method.Invoke(tab, null)!);
    }

    private static Task InvokeUserMutation(
        TournamentParticipantsTab tab,
        Func<Task<TournamentRegistrationDTO?>> action)
    {
        var method = typeof(TournamentParticipantsTab).GetMethod(
            "RunRegistrationActionAsync",
            PrivateInstance,
            binder: null,
            [typeof(Func<Task>), typeof(string), typeof(Guid), typeof(long)],
            modifiers: null)!;
        var requestGeneration = GetField<long>(tab, "_requestGeneration") + 1;
        SetPrivateField(tab, "_requestGeneration", requestGeneration);
        var tournamentId = tab.Tournament.Id;
        Func<Task> mutation = async () => await action();
        return (Task)method.Invoke(tab, [mutation, "The registration changed.", tournamentId, requestGeneration])!;
    }

    private static Task InvokeAdminMutation(
        TournamentParticipantsTab tab,
        AdminTournamentRegistrationDTO registration)
    {
        var method = typeof(TournamentParticipantsTab).GetMethod(
            "RemoveAdminRegistrationAsync",
            PrivateInstance)!;
        var testTab = (TestableTournamentParticipantsTab)tab;
        // This test invokes the mutation directly; skip the component's initial
        // data load so the render triggered by the mutation cannot start a second
        // lifecycle operation with unconfigured test services.
        SetPrivateField(tab, "_hasLoadedForTournament", true);
        return testTab.InvokeOnRendererAsync(() => (Task)method.Invoke(tab, [registration])!);
    }

    private static TournamentExtended CreateTournament(
        Guid id,
        params PublicTournamentRegistrationDTO[] registrations) => new()
        {
            Id = id,
            Name = id.ToString(),
            Status = TournamentStatus.Scheduled,
            ParticipationMode = ParticipationMode.Individual,
            Registrations = registrations
        };

    private static PublicTournamentRegistrationDTO CreatePublicRegistration(Guid id, Guid tournamentId) => new()
    {
        Id = id,
        TournamentId = tournamentId,
        Kind = TournamentRegistrationKind.Individual,
        Status = TournamentRegistrationStatus.Active,
        User = CreateUser(Guid.NewGuid(), $"user-{id:N}")
    };

    private static TournamentRegistrationDTO CreateRegistration(Guid id, Guid tournamentId) => new()
    {
        Id = id,
        TournamentId = tournamentId,
        Kind = TournamentRegistrationKind.Individual,
        Status = TournamentRegistrationStatus.Active,
        User = CreateUser(Guid.NewGuid(), $"user-{id:N}")
    };

    private static AdminTournamentRegistrationDTO CreateAdminRegistration(Guid id, Guid tournamentId) => new()
    {
        Id = id,
        TournamentId = tournamentId,
        Kind = TournamentRegistrationKind.Individual,
        Status = TournamentRegistrationStatus.Active,
        User = CreateUser(Guid.NewGuid(), $"admin-user-{id:N}")
    };

    private static PublicUserDTO CreateUser(Guid id, string username) => new()
    {
        Id = id,
        Username = username,
        DisplayName = username
    };

    private static TaskCompletionSource<T> NewCompletionSource<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void SetPrivateProperty(object instance, string name, object? value) =>
        typeof(TournamentParticipantsTab).GetProperty(name, PrivateInstance)!.SetValue(instance, value);

    private static void SetUpdatedCallback(
        TournamentParticipantsTab tab,
        Action<TournamentExtended> callback) =>
        typeof(TournamentParticipantsTab)
            .GetProperty(nameof(TournamentParticipantsTab.OnTournamentUpdated), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(tab, EventCallback.Factory.Create(tab, callback));

    private static void SetPrivateField(object instance, string name, object? value) =>
        typeof(TournamentParticipantsTab).GetField(name, PrivateInstance)!.SetValue(instance, value);

    private static T GetField<T>(object instance, string name) =>
        (T)typeof(TournamentParticipantsTab).GetField(name, PrivateInstance)!.GetValue(instance)!;

    private static object? GetField(object instance, string name) =>
        typeof(TournamentParticipantsTab).GetField(name, PrivateInstance)!.GetValue(instance);

    private sealed class TestableTournamentParticipantsTab : TournamentParticipantsTab
    {
        private readonly TestRenderer _renderer = new();

        public TestableTournamentParticipantsTab()
        {
            _renderer.Attach(this);
        }

        public void SetTournamentForTest(TournamentExtended tournament)
        {
            Tournament = tournament;
            OnParametersSet();
        }

        public Task InvokeOnRendererAsync(Func<Task> action) => _renderer.Dispatcher.InvokeAsync(action);

        public Task<T> InvokeOnRendererAsync<T>(Func<Task<T>> action) => _renderer.Dispatcher.InvokeAsync(action);
    }

    private sealed class TestRenderer : Renderer
    {
        public TestRenderer()
            : base(new ServiceCollection()
                .AddSingleton<IJSRuntime, TestJsRuntime>()
                .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
                .BuildServiceProvider(), NullLoggerFactory.Instance)
        {
        }

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public void Attach(IComponent component) => AssignRootComponentId(component);

        protected override void HandleException(Exception exception) => throw exception;

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }

    private sealed class TestJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            CreateResult<TValue>();

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            CreateResult<TValue>();

        private static ValueTask<TValue> CreateResult<TValue>()
        {
            if(typeof(TValue) == typeof(IJSObjectReference))
                return ValueTask.FromResult((TValue)(object)new TestJsObjectReference());

            return ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed class TestJsObjectReference : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public class RecordingToastServiceProxy : DispatchProxy
    {
        public List<string> Messages { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod is not null)
                Messages.Add(targetMethod.Name);

            return null;
        }
    }

    public class RecordingTournamentServiceProxy : DispatchProxy
    {
        public TaskCompletionSource<bool> RemovalCompletion { get; set; } = NewCompletionSource<bool>();
        public TaskCompletionSource<bool> CallStarted { get; } = NewCompletionSource<bool>();
        public List<(Guid TournamentId, Guid UserId, string? Reason)> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name == nameof(ITournamentService.RemoveTournamentUserRegistrationAsAdminAsync))
            {
                Calls.Add(((Guid)args![0]!, (Guid)args[1]!, (string?)args[2]));
                CallStarted.TrySetResult(true);
                return RemovalCompletion.Task;
            }

            throw new NotSupportedException($"Unexpected tournament service call: {targetMethod?.Name}");
        }
    }

    public class RosterEligibilityServiceProxy : DispatchProxy
    {
        public RosterCandidateEligibilityResponseDTO Eligibility { get; set; } = new() { Eligible = true };
        public Action? OnCheck { get; set; }
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var methodName = targetMethod?.Name ?? "<unknown>";
            Calls.Add(methodName);
            if(methodName == nameof(ITournamentService.CheckTeamRosterEligibilityAsync))
            {
                OnCheck?.Invoke();
                return Task.FromResult(Eligibility);
            }

            throw new NotSupportedException("Unexpected tournament service call: " + methodName);
        }
    }

    public class RecordingIndividualRegistrationServiceProxy : DispatchProxy
    {
        public CurrentUserTournamentRegistrationStateDTO State { get; set; } = new();
        public EligibilityResponseDTO Eligibility { get; set; } = new() { Eligible = true };
        public List<string> Calls { get; } = [];
        public List<Guid> RegisterTournamentIds { get; } = [];
        public List<Guid> DeleteTournamentIds { get; } = [];
        public int RegisterCallCount => RegisterTournamentIds.Count;
        public int DeleteCallCount => DeleteTournamentIds.Count;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var methodName = targetMethod?.Name ?? "<unknown>";
            Calls.Add(methodName);
            switch(methodName)
            {
                case nameof(ITournamentService.GetCurrentUserTournamentRegistrationStateAsync):
                    return Task.FromResult(State);
                case nameof(ITournamentService.CheckIndividualTournamentRegistrationEligibilityAsync):
                    return Task.FromResult(Eligibility);
                case nameof(ITournamentService.RegisterCurrentUserForTournamentAsync):
                    RegisterTournamentIds.Add((Guid)args![0]!);
                    return Task.FromResult(new TournamentRegistrationDTO { TournamentId = (Guid)args[0]! });
                case nameof(ITournamentService.DeleteCurrentUserTournamentRegistrationAsync):
                    DeleteTournamentIds.Add((Guid)args![0]!);
                    return Task.CompletedTask;
                default:
                    // The follow-up refresh is expected to fail in this harness; the
                    // mutation itself is what these tests count.
                    throw new NotSupportedException($"Unexpected tournament service call: {methodName}");
            }
        }
    }
}
