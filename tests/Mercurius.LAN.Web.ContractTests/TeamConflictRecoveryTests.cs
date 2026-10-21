using System.Net;
using System.Reflection;
using Blazored.Toast.Services;
using Microsoft.AspNetCore.Components;
using Mercurius.LAN.Web.Components.Pages.Teams;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.Services;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

/// <summary>
/// Focused coverage for the <c>team_changed</c> conflict recovery on the team management page:
/// only the exact conflict enters recovery, the rejected mutation is never resent, and a failed
/// confirmed-summary or notification refresh stays contained, recoverable, and retryable without
/// dropping the last confirmed state.
/// </summary>
public sealed class TeamConflictRecoveryTests
{
    private const string TeamChangedKey = "General.TeamManage.TeamChanged";
    private const string TeamChangedRefreshFailedKey = "General.TeamManage.TeamChangedRefreshFailed";

    [Fact]
    public async Task CreateIntentOpensAfterInteractiveRenderAndReplacesQueryOnlyOnce()
    {
        var (component, _) = CreateComponent(
            CreateTeamService(() => Task.FromResult(new CurrentUserTeamSummaryDTO())),
            new RecordingNotificationService(),
            new RecordingRealtimeService());
        var navigation = new RecordingNavigationManager();
        SetProperty(component, "NavigationManager", navigation);
        SetProperty(component, "CreateIntent", true);

        await InvokePrivateAsync(component, "OnAfterRenderAsync", true);
        await InvokePrivateAsync(component, "OnAfterRenderAsync", true);

        Assert.True(ReadField<bool>(component, "_isCreateTeamDialogOpen"));
        Assert.True(ReadField<bool>(component, "_createIntentConsumed"));
        Assert.Single(navigation.Navigations);
        Assert.Equal("/teams/manage", navigation.Navigations[0].Uri);
        Assert.True(navigation.Navigations[0].Replace);
    }

    [Fact]
    public async Task ExactTeamChangedConflictRefreshesSummaryAndNotificationsOnceWithoutResendingMutation()
    {
        var teamId = Guid.NewGuid();
        var summaryCalls = 0;
        var teamService = CreateTeamService(() =>
        {
            summaryCalls++;
            return Task.FromResult(SummaryWithTeam(teamId, "Confirmed team"));
        });
        var notifications = new RecordingNotificationService();
        var (component, toast) = CreateComponent(teamService, notifications, new RecordingRealtimeService());

        var mutationCalls = 0;
        await InvokeMutateAsync(component, () =>
        {
            mutationCalls++;
            return Task.FromException(TeamChangedConflict());
        });

        Assert.Equal(1, mutationCalls);
        Assert.Equal(1, summaryCalls);
        Assert.Equal(1, notifications.RefreshCalls);
        Assert.Contains(TeamChangedKey, toast.Warnings);
        Assert.False(IsTeamChangedRefreshFailed(component));
        Assert.Equal(teamId, Assert.Single(GetSummary(component).CaptainedTeams).Id);

        await component.DisposeAsync();
    }

    [Fact]
    public async Task NonConflictAndAuthorizationAndServerErrorsBypassConflictRecovery()
    {
        var scenarios = new[]
        {
            TeamServiceFailure(HttpStatusCode.Conflict, "team_changed_variant", "A different conflict."),
            TeamServiceFailure(HttpStatusCode.Unauthorized, null, "You need to sign in."),
            TeamServiceFailure(HttpStatusCode.Forbidden, null, "You are not allowed."),
            TeamServiceFailure(HttpStatusCode.InternalServerError, null, "The backend failed.")
        };

        foreach(var failure in scenarios)
        {
            var summaryCalls = 0;
            var teamService = CreateTeamService(() =>
            {
                summaryCalls++;
                return Task.FromResult(SummaryWithTeam(Guid.NewGuid(), "Should not load"));
            });
            var notifications = new RecordingNotificationService();
            var (component, toast) = CreateComponent(teamService, notifications, new RecordingRealtimeService());

            var mutationCalls = 0;
            await InvokeMutateAsync(component, () =>
            {
                mutationCalls++;
                return Task.FromException(failure);
            });

            Assert.Equal(1, mutationCalls);
            Assert.Equal(0, summaryCalls);
            Assert.Equal(0, notifications.RefreshCalls);
            Assert.DoesNotContain(TeamChangedKey, toast.Warnings);
            Assert.DoesNotContain(TeamChangedKey, toast.Errors);
            Assert.Contains(failure.Message, toast.Errors);
            Assert.False(IsTeamChangedRefreshFailed(component));

            await component.DisposeAsync();
        }
    }

    [Fact]
    public async Task FailedSummaryRefreshRetainsConflictContextAndLastConfirmedStateAndExposesRetry()
    {
        var confirmedTeamId = Guid.NewGuid();
        var summaryCalls = 0;
        var teamService = CreateTeamService(() =>
        {
            summaryCalls++;
            return Task.FromException<CurrentUserTeamSummaryDTO>(new TeamServiceException(
                "Summary unavailable.",
                "Load current user team summary",
                HttpStatusCode.ServiceUnavailable,
                null,
                new InvalidOperationException("backend offline")));
        });
        var notifications = new RecordingNotificationService();
        var (component, toast) = CreateComponent(teamService, notifications, new RecordingRealtimeService());
        SetSummary(component, SummaryWithTeam(confirmedTeamId, "Last confirmed"));

        var mutationCalls = 0;
        await InvokeMutateAsync(component, () =>
        {
            mutationCalls++;
            return Task.FromException(TeamChangedConflict());
        });

        // The conflict feedback stays visible, the last confirmed state is preserved, and the
        // recoverable retry state is exposed even though the summary refresh failed.
        Assert.Equal(1, mutationCalls);
        Assert.Equal(1, summaryCalls);
        Assert.Contains(TeamChangedKey, toast.Warnings);
        Assert.True(IsTeamChangedRefreshFailed(component));
        Assert.True(component.RenderCount > 0);
        Assert.Equal(confirmedTeamId, Assert.Single(GetSummary(component).CaptainedTeams).Id);
        Assert.Equal(0, notifications.RefreshCalls);

        await component.DisposeAsync();
    }

    [Fact]
    public async Task FailedNotificationRefreshIsContainedSeparatelyAndRecoverableWithoutLosingConfirmState()
    {
        var confirmedTeamId = Guid.NewGuid();
        var refreshedTeamId = Guid.NewGuid();
        var summaryCalls = 0;
        var teamService = CreateTeamService(() =>
        {
            summaryCalls++;
            return Task.FromResult(SummaryWithTeam(refreshedTeamId, "Refreshed"));
        });

        // The notification refresh fails while the confirmed-summary reload succeeds.
        var notifications = new RecordingNotificationService { FailuresBeforeSuccess = 1 };
        var (component, toast) = CreateComponent(teamService, notifications, new RecordingRealtimeService());
        SetSummary(component, SummaryWithTeam(confirmedTeamId, "Last confirmed"));

        var mutationCalls = 0;
        await InvokeMutateAsync(component, () =>
        {
            mutationCalls++;
            return Task.FromException(TeamChangedConflict());
        });

        Assert.Equal(1, mutationCalls);
        Assert.Equal(1, summaryCalls);
        Assert.Equal(1, notifications.RefreshCalls);
        Assert.Contains(TeamChangedKey, toast.Warnings);
        Assert.True(IsTeamChangedRefreshFailed(component));
        // The refreshed summary is available even though the notification refresh failed.
        Assert.Equal(refreshedTeamId, Assert.Single(GetSummary(component).CaptainedTeams).Id);

        await component.DisposeAsync();
    }

    [Fact]
    public async Task ManualRetryRefreshesOnlyAndNeverDuplicatesTheRejectedMutation()
    {
        var confirmedTeamId = Guid.NewGuid();
        var refreshedTeamId = Guid.NewGuid();
        var summaryCalls = 0;
        var teamService = CreateTeamService(() =>
        {
            summaryCalls++;
            return summaryCalls == 1
                ? Task.FromException<CurrentUserTeamSummaryDTO>(new TeamServiceException(
                    "Summary unavailable.",
                    "Load current user team summary",
                    HttpStatusCode.ServiceUnavailable,
                    null,
                    new InvalidOperationException("backend offline")))
                : Task.FromResult(SummaryWithTeam(refreshedTeamId, "Refreshed"));
        });
        var notifications = new RecordingNotificationService();
        var (component, _) = CreateComponent(teamService, notifications, new RecordingRealtimeService());
        SetSummary(component, SummaryWithTeam(confirmedTeamId, "Last confirmed"));

        var mutationCalls = 0;
        await InvokeMutateAsync(component, () =>
        {
            mutationCalls++;
            return Task.FromException(TeamChangedConflict());
        });

        Assert.True(IsTeamChangedRefreshFailed(component));

        // Retrying only re-runs the confirmed-summary and notification refresh.
        await InvokePrivateAsync(component, "RefreshAfterTeamChangedAsync");

        Assert.Equal(1, mutationCalls);
        Assert.Equal(2, summaryCalls);
        Assert.Equal(1, notifications.RefreshCalls);
        Assert.False(IsTeamChangedRefreshFailed(component));
        Assert.Equal(refreshedTeamId, Assert.Single(GetSummary(component).CaptainedTeams).Id);

        await component.DisposeAsync();
    }

    [Fact]
    public async Task ConflictRecoveryAfterDisposalDoesNotThrow()
    {
        var teamService = CreateTeamService(() => Task.FromResult(SummaryWithTeam(Guid.NewGuid(), "Team")));
        var notifications = new RecordingNotificationService();
        var (component, _) = CreateComponent(teamService, notifications, new RecordingRealtimeService());
        await component.DisposeAsync();

        var mutationCalls = 0;
        await InvokeMutateAsync(component, () =>
        {
            mutationCalls++;
            return Task.FromException(TeamChangedConflict());
        });

        Assert.Equal(1, mutationCalls);
    }

    [Fact]
    public async Task SummaryReturnedAfterDisposalDoesNotReplaceConfirmedStateOrNotifyOrRender()
    {
        var pendingSummary = new TaskCompletionSource<CurrentUserTeamSummaryDTO>(TaskCreationOptions.RunContinuationsAsynchronously);
        var teamService = CreateTeamService(() => pendingSummary.Task);
        var notifications = new RecordingNotificationService();
        var realtime = new RecordingRealtimeService();
        var (component, _) = CreateComponent(teamService, notifications, realtime);
        var lastConfirmed = SummaryWithTeam(Guid.NewGuid(), "Last confirmed");
        SetSummary(component, lastConfirmed);

        var refresh = InvokePrivateAsync(component, "RefreshSummaryAsync", CancellationToken.None);
        await component.DisposeAsync();
        pendingSummary.SetResult(SummaryWithTeam(Guid.NewGuid(), "Late response"));
        await refresh;

        Assert.Same(lastConfirmed, GetSummary(component));
        Assert.Equal(0, notifications.RefreshCalls);
        Assert.Equal(0, realtime.JoinCalls);
        Assert.Equal(0, component.RenderCount);
    }

    [Fact]
    public void ConflictRecoveryUiKeepsLocalizedConflictAndRefreshFailureContextWithRetryAction()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Teams/ManageTeams.razor");

        Assert.Contains("@if(_teamChangedRefreshFailed)", markup, StringComparison.Ordinal);
        Assert.Contains($"@Localization[\"{TeamChangedKey}\"]", markup, StringComparison.Ordinal);
        Assert.Contains($"@Localization[\"{TeamChangedRefreshFailedKey}\"]", markup, StringComparison.Ordinal);
        Assert.Contains("status.tryAgain", markup, StringComparison.Ordinal);
        Assert.Contains("@onclick=\"RefreshAfterTeamChangedAsync\"", markup, StringComparison.Ordinal);
    }

    private static TeamServiceException TeamChangedConflict() =>
        new(
            "This team changed while you were working.",
            "Invite user",
            HttpStatusCode.Conflict,
            null,
            new InvalidOperationException("conflict"),
            code: "team_changed");

    private static TeamServiceException TeamServiceFailure(HttpStatusCode statusCode, string? code, string message) =>
        new(
            message,
            "Team mutation",
            statusCode,
            null,
            new InvalidOperationException(message),
            code);

    private static CurrentUserTeamSummaryDTO SummaryWithTeam(Guid id, string name) =>
        new()
        {
            CaptainedTeams = [new TeamManagementSummaryDTO { Id = id, Name = name }]
        };

    private static (TestManageTeams Component, RecordingToastProxy Toast) CreateComponent(
        ITeamService teamService,
        ITeamNotificationService notificationService,
        ITeamRealtimeService realtimeService)
    {
        var component = new TestManageTeams();
        var toastProxy = DispatchProxy.Create<IToastService, RecordingToastProxy>();
        var toast = (RecordingToastProxy)(object)toastProxy;
        SetProperty(component, "TeamService", teamService);
        SetProperty(component, "NotificationService", notificationService);
        SetProperty(component, "RealtimeService", realtimeService);
        SetProperty(component, "ToastService", toastProxy);
        SetProperty(component, "Localization", TestLocalizationService.Instance);
        return (component, toast);
    }

    private static ITeamService CreateTeamService(Func<Task<CurrentUserTeamSummaryDTO>> summary)
    {
        return CreateProxy<ITeamService>((method, _) => method.Name == nameof(ITeamService.GetCurrentUserTeamSummaryAsync)
            ? summary()
            : throw new NotSupportedException($"Unexpected team service call: {method.Name}"));
    }

    private static Task InvokeMutateAsync(ManageTeams component, Func<Task> action) =>
        InvokePrivateAsync(component, "MutateAsync", action);

    private static async Task InvokePrivateAsync(ManageTeams component, string methodName, params object?[] arguments)
    {
        var method = typeof(ManageTeams).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found on {nameof(ManageTeams)}.");

        if(method.Invoke(component, arguments) is Task task)
            await task;
    }

    private static bool IsTeamChangedRefreshFailed(ManageTeams component) =>
        ReadField<bool>(component, "_teamChangedRefreshFailed");

    private static CurrentUserTeamSummaryDTO GetSummary(ManageTeams component) =>
        ReadField<CurrentUserTeamSummaryDTO>(component, "_summary");

    private static void SetSummary(ManageTeams component, CurrentUserTeamSummaryDTO summary) =>
        SetField(component, "_summary", summary);

    private static T ReadField<T>(ManageTeams component, string fieldName) =>
        (T)(typeof(ManageTeams).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(component)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on {nameof(ManageTeams)}."));

    private static void SetField(ManageTeams component, string fieldName, object? value) =>
        (typeof(ManageTeams).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on {nameof(ManageTeams)}."))
        .SetValue(component, value);

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

    private static T CreateProxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, ServiceProxy>();
        ((ServiceProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(path))
                return File.ReadAllText(path);

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate '{relativePath}'.");
    }

    private class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = (_, _) => null;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Missing proxy method."), args);
    }

    // Not sealed: DispatchProxy.Create derives a runtime proxy subclass from this type.
    private class RecordingToastProxy : DispatchProxy
    {
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Successes { get; } = [];
        public List<string> Infos { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(args is { Length: > 0 } && args[0] is string message)
            {
                switch(targetMethod?.Name)
                {
                    case nameof(IToastService.ShowWarning):
                        Warnings.Add(message);
                        break;
                    case nameof(IToastService.ShowError):
                        Errors.Add(message);
                        break;
                    case nameof(IToastService.ShowSuccess):
                        Successes.Add(message);
                        break;
                    case nameof(IToastService.ShowInfo):
                        Infos.Add(message);
                        break;
                }
            }

            return null;
        }
    }

    private sealed class RecordingNotificationService : ITeamNotificationService
    {
        public event Func<Task>? Changed;
        public event Func<Guid, Task>? RosterDecisionChanged;

        public IReadOnlyList<TeamNotificationItem> Notifications => [];
        public int UnreadCount => 0;

        public int FailuresBeforeSuccess { get; init; }
        public int RefreshCalls { get; private set; }
        private int _failures;

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            if(_failures < FailuresBeforeSuccess)
            {
                _failures++;
                return Task.FromException(new TeamServiceException(
                    "Notifications unavailable.",
                    "Refresh team notifications",
                    HttpStatusCode.ServiceUnavailable,
                    null,
                    new InvalidOperationException("notifications offline")));
            }

            return Task.CompletedTask;
        }

        public Task RespondToRosterSelectionAsync(string notificationId, bool accept, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkAllReadAsync() => Task.CompletedTask;

        public Task DismissAsync(string id) => Task.CompletedTask;
    }

    private sealed class RecordingRealtimeService : ITeamRealtimeService
    {
        public event Func<Task>? TeamStateInvalidated;
        public bool IsConnected => false;
        public int JoinCalls { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task JoinTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default)
        {
            JoinCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestManageTeams : ManageTeams
    {
        public int RenderCount { get; private set; }

        protected override Task RequestRenderAsync()
        {
            RenderCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public List<(string Uri, bool Replace)> Navigations { get; } = [];

        public RecordingNavigationManager() => Initialize("http://localhost/", "http://localhost/teams/manage?create=true");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Navigations.Add((uri, options.ReplaceHistoryEntry));
    }
}
