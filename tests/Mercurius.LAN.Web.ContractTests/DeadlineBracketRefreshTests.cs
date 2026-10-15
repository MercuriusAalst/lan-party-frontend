using System.Reflection;
using Mercurius.LAN.Web.Components.Pages.Tournaments;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class DeadlineBracketRefreshTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void ExpiredScoreConfirmationTransitionIsRecognizedOnlyAfterBackendVersionAdvances()
    {
        var now = DateTime.UtcNow;
        var previous = new Match
        {
            Id = Guid.NewGuid(),
            LifecycleState = MatchLifecycleState.ScoreConfirmation,
            ScoreConfirmationDeadlineUtc = now.AddSeconds(-1),
            ResultVersion = 7
        };
        var completed = new Match
        {
            Id = previous.Id,
            LifecycleState = MatchLifecycleState.Completed,
            ResultVersion = 8
        };

        Assert.True(TournamentDetail.ConfirmsExpiredDeadlineTransition(previous, completed, now));
        Assert.False(TournamentDetail.ConfirmsExpiredDeadlineTransition(
            previous,
            new Match { Id = previous.Id, LifecycleState = MatchLifecycleState.Completed, ResultVersion = 7 },
            now));
        Assert.False(TournamentDetail.ConfirmsExpiredDeadlineTransition(
            new Match
            {
                Id = previous.Id,
                LifecycleState = previous.LifecycleState,
                ScoreConfirmationDeadlineUtc = now.AddSeconds(5),
                ResultVersion = previous.ResultVersion
            },
            completed,
            now));
    }

    [Fact]
    public void ExpiredCorrectionTransitionIsRecognizedFromTheBackendProjection()
    {
        var now = DateTime.UtcNow;
        var previous = new Match
        {
            Id = Guid.NewGuid(),
            LifecycleState = MatchLifecycleState.Disputed,
            CorrectionDeadlineUtc = now.AddSeconds(-1),
            ResultVersion = 12
        };
        var resolved = new Match
        {
            Id = previous.Id,
            LifecycleState = MatchLifecycleState.AdminResolutionRequired,
            ResultVersion = 13
        };

        Assert.True(TournamentDetail.ConfirmsExpiredDeadlineTransition(previous, resolved, now));
    }

    [Fact]
    public void FreshSameIdProjectionSurvivesStaleSuccessfulParentSnapshotAndNewerVersionWins()
    {
        var tournamentId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var freshMatch = new Match { Id = matchId, ResultVersion = 9, LifecycleState = MatchLifecycleState.Completed };
        var staleSnapshot = new TournamentExtended
        {
            Id = tournamentId,
            Matches = [new Match { Id = matchId, ResultVersion = 8, LifecycleState = MatchLifecycleState.Disputed }]
        };

        var preserved = TournamentDetail.PreserveFresherMatchProjections(
            new TournamentExtended { Id = tournamentId, Matches = [freshMatch] },
            staleSnapshot);

        Assert.Same(freshMatch, preserved.Matches.Single());

        var newerMatch = new Match { Id = matchId, ResultVersion = 10, LifecycleState = MatchLifecycleState.AdminResolutionRequired };
        var newerSnapshot = TournamentDetail.PreserveFresherMatchProjections(
            new TournamentExtended { Id = tournamentId, Matches = [freshMatch] },
            new TournamentExtended { Id = tournamentId, Matches = [newerMatch] });

        Assert.Same(newerMatch, newerSnapshot.Matches.Single());
    }

    [Fact]
    public async Task DeadlineRefreshCoalescesMatchesAndSurvivesDialogCloseUntilAggregateConverges()
    {
        var tournamentId = Guid.NewGuid();
        var before = CreateExpiredConfirmationMatch(resultVersion: 3);
        var secondBefore = CreateExpiredConfirmationMatch(resultVersion: 6);
        var effective = new Match { Id = before.Id, ResultVersion = 4, LifecycleState = MatchLifecycleState.Completed };
        var secondEffective = new Match { Id = secondBefore.Id, ResultVersion = 7, LifecycleState = MatchLifecycleState.Disputed };
        var calls = 0;
        var service = CreateTournamentService((id, _) =>
        {
            var call = Interlocked.Increment(ref calls);
            var snapshotMatches = call == 1
                ? new[] { CloneMatch(before), CloneMatch(secondBefore) }
                : new[] { CloneMatch(effective), CloneMatch(secondEffective) };
            return Task.FromResult<TournamentExtended?>(new TournamentExtended { Id = id, Matches = snapshotMatches });
        });
        var page = CreatePage(
            tournamentId,
            new TournamentExtended { Id = tournamentId, Matches = [before, secondBefore] },
            service);

        Assert.True(page.ApplyMatchRefresh(effective));
        var refreshTask = page.RefreshTask;
        Assert.NotNull(refreshTask);
        Assert.True(page.ApplyMatchRefresh(secondEffective));
        await page.CloseDialogAsync();
        Assert.Null(GetField<Match?>(page, "_selectedMatch"));
        Assert.Equal(2, PendingMatches(page).Count);

        page.ReleasePoll();
        await WaitForAsync(() => page.RenderCount >= 1);
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.Same(effective, GetTournament(page).Matches.Single(match => match.Id == effective.Id));
        Assert.Same(secondEffective, GetTournament(page).Matches.Single(match => match.Id == secondEffective.Id));
        Assert.Equal(2, PendingMatches(page).Count);

        page.ReleasePoll();
        await refreshTask!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, Volatile.Read(ref calls));
        Assert.Equal(0, PendingMatches(page).Count);
        Assert.False(GetField<bool>(page, "_deadlineBracketRefreshFailed"));
        Assert.Equal(4, GetTournament(page).Matches.Single(match => match.Id == effective.Id).ResultVersion);
        Assert.Equal(7, GetTournament(page).Matches.Single(match => match.Id == secondEffective.Id).ResultVersion);
    }

    [Fact]
    public async Task LateStalePollResponseDoesNotReplaceNewerMatchProjection()
    {
        var tournamentId = Guid.NewGuid();
        var before = CreateExpiredConfirmationMatch(resultVersion: 3);
        var effective = new Match { Id = before.Id, ResultVersion = 4, LifecycleState = MatchLifecycleState.Completed };
        var superseding = new Match { Id = before.Id, ResultVersion = 5, LifecycleState = MatchLifecycleState.Disputed };
        var staleResponse = new TaskCompletionSource<TournamentExtended?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var service = CreateTournamentService((id, _) =>
        {
            var call = Interlocked.Increment(ref calls);
            if(call == 1)
            {
                requestStarted.TrySetResult();
                return staleResponse.Task;
            }

            return Task.FromResult<TournamentExtended?>(new TournamentExtended
            {
                Id = id,
                Matches = [CloneMatch(superseding)]
            });
        });
        var page = CreatePage(tournamentId, new TournamentExtended { Id = tournamentId, Matches = [before] }, service);
        Assert.True(page.ApplyMatchRefresh(effective));
        var refreshTask = page.RefreshTask!;

        page.ReleasePoll();
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        SetField(page, "_tournament", new TournamentExtended
        {
            Id = tournamentId,
            Matches = [CloneMatch(superseding)]
        });
        staleResponse.TrySetResult(new TournamentExtended
        {
            Id = tournamentId,
            Matches = [CloneMatch(before)]
        });
        await WaitForAsync(() => page.WaitCount >= 2);
        Assert.Equal(0, page.RenderCount);
        Assert.Equal(5, GetTournament(page).Matches.Single().ResultVersion);

        page.ReleasePoll();
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, Volatile.Read(ref calls));
        Assert.Equal(0, PendingMatches(page).Count);
        Assert.Equal(5, GetTournament(page).Matches.Single().ResultVersion);
        Assert.Equal(MatchLifecycleState.Disputed, GetTournament(page).Matches.Single().LifecycleState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NavigationOrDisposalCancelsInFlightRefreshAndDiscardsLateSnapshot(bool dispose)
    {
        var tournamentId = Guid.NewGuid();
        var before = CreateExpiredConfirmationMatch(resultVersion: 3);
        var effective = new Match { Id = before.Id, ResultVersion = 4, LifecycleState = MatchLifecycleState.Completed };
        var request = new TaskCompletionSource<TournamentExtended?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateTournamentService((id, token) =>
        {
            requestStarted.TrySetResult(token);
            return request.Task;
        });
        var page = CreatePage(tournamentId, new TournamentExtended { Id = tournamentId, Matches = [before] }, service);
        Assert.True(page.ApplyMatchRefresh(effective));
        var refreshTask = page.RefreshTask!;

        page.ReleasePoll();
        var requestToken = await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if(dispose)
        {
            page.Dispose();
        }
        else
        {
            page.NavigateTo(Guid.NewGuid());
        }

        Assert.True(requestToken.IsCancellationRequested);
        request.TrySetResult(new TournamentExtended { Id = tournamentId, Matches = [CloneMatch(before)] });
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, PendingMatches(page).Count);
        if(dispose)
        {
            Assert.Same(effective, GetTournament(page).Matches.Single());
        }
        else
        {
            Assert.Null(GetField<TournamentExtended?>(page, "_tournament"));
        }
        Assert.Equal(0, page.RenderCount);
    }

    [Fact]
    public async Task ExhaustionShowsRecoverableFailureAndRetryCanConverge()
    {
        var tournamentId = Guid.NewGuid();
        var before = CreateExpiredConfirmationMatch(resultVersion: 3);
        var effective = new Match { Id = before.Id, ResultVersion = 4, LifecycleState = MatchLifecycleState.Completed };
        var calls = 0;
        var retry = 0;
        var service = CreateTournamentService((id, _) =>
        {
            Interlocked.Increment(ref calls);
            if(Volatile.Read(ref retry) == 0)
                return Task.FromException<TournamentExtended?>(new InvalidOperationException("Temporary aggregate failure."));

            var snapshotMatch = CloneMatch(effective);
            return Task.FromResult<TournamentExtended?>(new TournamentExtended { Id = id, Matches = [snapshotMatch] });
        });
        var page = CreatePage(
            tournamentId,
            new TournamentExtended { Id = tournamentId, Matches = [before] },
            service);

        Assert.True(page.ApplyMatchRefresh(effective));
        var refreshTask = page.RefreshTask!;
        for(var attempt = 0; attempt < 39; attempt++)
        {
            page.ReleasePoll();
            var nextWait = attempt + 2;
            await WaitForAsync(() => page.WaitCount >= nextWait);
        }

        page.ReleasePoll();
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(40, Volatile.Read(ref calls));
        Assert.True(GetField<bool>(page, "_deadlineBracketRefreshFailed"));
        Assert.Equal(1, PendingMatches(page).Count);

        Volatile.Write(ref retry, 1);
        await page.RetryRefreshAsync();
        var retryTask = page.RefreshTask!;
        page.ReleasePoll();
        await retryTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(41, Volatile.Read(ref calls));
        Assert.False(GetField<bool>(page, "_deadlineBracketRefreshFailed"));
        Assert.Empty(PendingMatches(page));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalRenderFailureDuringTeardownDoesNotFaultRefreshTaskOrMutateState(bool cancellation)
    {
        var tournamentId = Guid.NewGuid();
        var before = CreateExpiredConfirmationMatch(resultVersion: 3);
        var effective = new Match { Id = before.Id, ResultVersion = 4, LifecycleState = MatchLifecycleState.Completed };
        var service = CreateTournamentService((_, _) =>
            Task.FromException<TournamentExtended?>(new InvalidOperationException("Temporary aggregate failure.")));
        var page = CreatePage(tournamentId, new TournamentExtended { Id = tournamentId, Matches = [before] }, service);
        Assert.True(page.ApplyMatchRefresh(effective));
        var refreshTask = page.RefreshTask!;
        page.FailRenders(cancellation);

        for(var attempt = 0; attempt < 39; attempt++)
        {
            page.ReleasePoll();
            var nextWait = attempt + 2;
            await WaitForAsync(() => page.WaitCount >= nextWait);
        }

        page.ReleasePoll();
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, page.RenderCount);
        Assert.Null(GetField<CancellationTokenSource?>(page, "_deadlineBracketRefreshCancellation"));
        Assert.Null(GetField<Task?>(page, "_deadlineBracketRefreshTask"));
        Assert.True(GetField<bool>(page, "_deadlineBracketRefreshFailed"));
        Assert.Equal(1, PendingMatches(page).Count);
        Assert.Equal(4, GetTournament(page).Matches.Single().ResultVersion);
    }

    private static Match CreateExpiredConfirmationMatch(int resultVersion) => new()
    {
        Id = Guid.NewGuid(),
        LifecycleState = MatchLifecycleState.ScoreConfirmation,
        ScoreConfirmationDeadlineUtc = DateTime.UtcNow.AddMinutes(-1),
        ResultVersion = resultVersion
    };

    private static Match CloneMatch(Match match) => new()
    {
        Id = match.Id,
        LifecycleState = match.LifecycleState,
        ScoreConfirmationDeadlineUtc = match.ScoreConfirmationDeadlineUtc,
        CorrectionDeadlineUtc = match.CorrectionDeadlineUtc,
        ResultVersion = match.ResultVersion
    };

    private static TournamentDetailTestPage CreatePage(
        Guid tournamentId,
        TournamentExtended tournament,
        ITournamentService service)
    {
        var page = new TournamentDetailTestPage { TournamentId = tournamentId };
        SetField(page, "_tournament", tournament);
        SetField(page, "_isLoading", false);
        SetPrivateProperty(page, "TournamentService", service);
        return page;
    }

    private static TournamentExtended GetTournament(TournamentDetailTestPage page) =>
        GetField<TournamentExtended>(page, "_tournament");

    private static Dictionary<Guid, int> PendingMatches(TournamentDetailTestPage page) =>
        GetField<Dictionary<Guid, int>>(page, "_pendingDeadlineMatchVersions");

    private static ITournamentService CreateTournamentService(
        Func<Guid, CancellationToken, Task<TournamentExtended?>> handler)
    {
        var proxy = DispatchProxy.Create<ITournamentService, TournamentServiceProxy>();
        ((TournamentServiceProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static bool InvokePrivateBool(object instance, string name, params object?[] args) =>
        (bool)(FindMethod(instance.GetType(), name).Invoke(instance, args)
            ?? throw new InvalidOperationException($"Method '{name}' returned null."));

    private static Task InvokePrivateTask(object instance, string name, params object?[] args) =>
        (Task)(FindMethod(instance.GetType(), name).Invoke(instance, args)
            ?? throw new InvalidOperationException($"Method '{name}' returned null."));

    private static MethodInfo FindMethod(Type type, string name) =>
        FindMethodInHierarchy(type, name)
        ?? throw new MissingMethodException(type.FullName, name);

    private static MethodInfo? FindMethodInHierarchy(Type? type, string name)
    {
        for(var current = type; current is not null; current = current.BaseType)
        {
            var method = current.GetMethod(
                name,
                PrivateInstance | BindingFlags.DeclaredOnly);
            if(method is not null)
                return method;
        }

        return null;
    }

    private static void SetPrivateProperty(object instance, string name, object? value)
    {
        var property = instance.GetType().BaseType!.GetProperty(name, PrivateInstance)
            ?? throw new MissingMemberException(instance.GetType().BaseType!.FullName, name);
        property.SetValue(instance, value);
    }

    private static void SetField(object instance, string name, object? value) =>
        GetFieldInfo(instance, name).SetValue(instance, value);

    private static T GetField<T>(object instance, string name) =>
        (T)GetFieldInfo(instance, name).GetValue(instance)!;

    private static FieldInfo GetFieldInfo(object instance, string name) =>
        instance.GetType().BaseType!.GetField(name, PrivateInstance)
        ?? throw new MissingFieldException(instance.GetType().BaseType!.FullName, name);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while(!condition())
            await Task.Delay(10, timeout.Token);
    }

    private sealed class TournamentDetailTestPage : TournamentDetail
    {
        private readonly SemaphoreSlim _pollGate = new(0);
        private int _renderCount;
        private int _waitCount;
        private bool _failRender;
        private bool _cancelRender;

        public int RenderCount => Volatile.Read(ref _renderCount);
        public int WaitCount => Volatile.Read(ref _waitCount);
        public Task? RefreshTask => GetField<Task?>(this, "_deadlineBracketRefreshTask");

        public bool ApplyMatchRefresh(Match match) => InvokePrivateBool(this, "TryApplyRefreshedMatchProjection", match);

        public Task CloseDialogAsync() => InvokePrivateTask(this, "CloseMatchDetailsAsync");

        public Task RetryRefreshAsync() => InvokePrivateTask(this, "RetryDeadlineBracketRefreshAsync");

        public void NavigateTo(Guid tournamentId)
        {
            TournamentId = tournamentId;
            FindMethod(typeof(TournamentDetail), "ResetForTournamentChange").Invoke(this, null);
        }

        public void ReleasePoll() => _pollGate.Release();

        public void FailRenders(bool cancellation)
        {
            _cancelRender = cancellation;
            _failRender = true;
        }

        protected override async Task WaitForDeadlineBracketRefreshAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _waitCount);
            await _pollGate.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        protected override Task RequestDeadlineBracketRefreshRenderAsync()
        {
            Interlocked.Increment(ref _renderCount);
            if(!_failRender)
                return Task.CompletedTask;

            return _cancelRender
                ? Task.FromCanceled(new CancellationToken(canceled: true))
                : Task.FromException(new ObjectDisposedException("renderer"));
        }
    }

    public class TournamentServiceProxy : DispatchProxy
    {
        public Func<Guid, CancellationToken, Task<TournamentExtended?>> Handler { get; set; } =
            (_, _) => Task.FromResult<TournamentExtended?>(null);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name != nameof(ITournamentService.GetTournamentByIdAsync))
                throw new NotSupportedException($"Unexpected tournament service call: {targetMethod?.Name}");

            return Handler((Guid)args![0]!, (CancellationToken)args[1]!);
        }
    }
}
