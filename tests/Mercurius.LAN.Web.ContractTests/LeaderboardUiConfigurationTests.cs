using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Pages.Tournaments;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Leaderboard;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Search;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class LeaderboardUiConfigurationTests
{
    [Theory]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("15,0")]
    [InlineData("0")]
    [InlineData("-1500")]
    [InlineData("abc")]
    public void DurationInputRejectsAnythingButPositiveWholeMilliseconds(string input)
    {
        var dialog = CreateDialog(LeaderboardRankingMetric.FastestTime);

        var accepted = dialog.TryBuildValue(input, out var score, out var durationMilliseconds, out var error);

        Assert.False(accepted);
        Assert.Null(score);
        Assert.Null(durationMilliseconds);
        Assert.Equal("Feature.leaderboard.validationDuration", error);
    }

    [Fact]
    public void DurationInputKeepsWholeMillisecondsUntouched()
    {
        var dialog = CreateDialog(LeaderboardRankingMetric.FastestTime);

        var accepted = dialog.TryBuildValue("1500", out var score, out var durationMilliseconds, out var error);

        Assert.True(accepted);
        Assert.Null(score);
        Assert.Equal(1500, durationMilliseconds);
        Assert.Null(error);
    }

    [Fact]
    public async Task RecordingAFractionalDurationNeverReachesTheBackend()
    {
        var dialog = CreateDialog(LeaderboardRankingMetric.FastestTime);
        SetField(dialog, "_mode", Enum.Parse(GetNestedEnum(dialog.GetType(), "EntryMode"), "Guest"));
        SetField(dialog, "_guestName", "Fractional Guest");
        SetField(dialog, "_valueInput", "1.5");
        var tournamentService = CreateRecordingTournamentService(out var calls);
        SetPrivateProperty(dialog, "TournamentService", tournamentService);

        await InvokePrivateTask(dialog, "SubmitAttemptAsync");

        Assert.Empty(calls);
        Assert.False(GetField<bool>(dialog, "_isSubmitting"));
        Assert.Equal("Feature.leaderboard.validationDuration", GetField<string?>(dialog, "_submitError"));
    }

    [Fact]
    public async Task CorrectingToAFractionalDurationNeverReachesTheBackend()
    {
        var dialog = CreateDialog(LeaderboardRankingMetric.FastestTime);
        SetField(dialog, "_editValueInput", "1.5");
        var tournamentService = CreateRecordingTournamentService(out var calls);
        SetPrivateProperty(dialog, "TournamentService", tournamentService);
        var attempt = new LeaderboardAttemptDTO
        {
            Id = Guid.NewGuid(),
            DurationMilliseconds = 39_480,
            RowVersion = Guid.NewGuid()
        };

        await InvokePrivateTask(dialog, "SaveAttemptEditAsync", attempt);

        Assert.Empty(calls);
        Assert.False(GetField<bool>(dialog, "_isSavingAttempt"));
        Assert.Equal("Feature.leaderboard.validationDuration", GetField<string?>(dialog, "_submitError"));
    }

    [Fact]
    public void SwitchingAwayFromLeaderboardClearsTheCreateRankingMetric()
    {
        var dialog = new AddTournamentDialog();
        var dto = GetField<CreateTournamentDTO>(dialog, "_newTournament");
        dto.BracketType = BracketType.Leaderboard;
        dto.LeaderboardRankingMetric = LeaderboardRankingMetric.FastestTime;
        dto.ParticipationMode = ParticipationMode.Team;
        dto.TeamSize = 4;

        dialog.ApplyBracketTypeDefaults();

        Assert.Equal(LeaderboardRankingMetric.FastestTime, dto.LeaderboardRankingMetric);
        Assert.Equal(ParticipationMode.Individual, dto.ParticipationMode);
        Assert.Null(dto.TeamSize);

        dto.BracketType = BracketType.SingleElimination;

        dialog.ApplyBracketTypeDefaults();

        Assert.Null(dto.LeaderboardRankingMetric);
    }

    [Fact]
    public void SwitchingAwayFromLeaderboardClearsTheEditRankingMetric()
    {
        var tab = new TournamentOverviewTab();
        var dto = GetField<UpdateTournamentDTO>(tab, "_editTournament");
        dto.BracketType = BracketType.Leaderboard;
        dto.LeaderboardRankingMetric = LeaderboardRankingMetric.HighestScore;
        dto.ParticipationMode = ParticipationMode.Team;
        dto.TeamSize = 3;

        tab.ApplyBracketTypeDefaults();

        Assert.Equal(LeaderboardRankingMetric.HighestScore, dto.LeaderboardRankingMetric);
        Assert.Equal(ParticipationMode.Individual, dto.ParticipationMode);
        Assert.Null(dto.TeamSize);

        dto.BracketType = BracketType.DoubleElimination;

        tab.ApplyBracketTypeDefaults();

        Assert.Null(dto.LeaderboardRankingMetric);
    }

    [Theory]
    [InlineData("""{"code":"leaderboard_changed","message":"The tournament leaderboard changed."}""", "Feature.tournaments.editConflict")]
    [InlineData("{\"code\":\"leaderboard_changed\"", "Feature.tournaments.updateFailed")]
    [InlineData("", "Feature.tournaments.updateFailed")]
    [InlineData("""{"code":"something_else"}""", "Feature.tournaments.updateFailed")]
    public void TournamentEditConflictShowsALocalizedMessageAndNeverRawApiContent(string content, string expectedKey)
    {
        var tab = new TournamentOverviewTab();
        SetPrivateProperty(tab, "Localization", TestLocalizationService.Instance);

        var message = tab.ResolveEditSaveError(CreateApiException(HttpStatusCode.Conflict, content));

        Assert.Equal(expectedKey, message);
        Assert.NotEqual(content, message);
        Assert.DoesNotContain("{", message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyLeaderboardCompletionKeepsTheBackendReasonAndStaysInProgress()
    {
        var detail = CreateTournamentDetail();

        Assert.True(TournamentDetail.IndicatesEmptyLeaderboardCompletion(
            "A leaderboard tournament requires at least one valid recorded result before completion."));
        Assert.True(TournamentDetail.IndicatesEmptyLeaderboardCompletion(
            "A leaderboard tournament requires at least one recorded result to be completed."));
        Assert.False(TournamentDetail.IndicatesEmptyLeaderboardCompletion(
            "Tournament has to be in progress to be able to complete"));
        Assert.False(TournamentDetail.IndicatesEmptyLeaderboardCompletion(null));

        Assert.Equal(
            "Feature.tournaments.completionRequiresResult",
            detail.ResolveTournamentActionError(
                CreateApiException(
                    HttpStatusCode.BadRequest,
                    "\"A leaderboard tournament requires at least one valid recorded result before completion.\"")));

        Assert.Equal(
            "Feature.tournaments.completionRequiresResult",
            detail.ResolveTournamentActionError(
                CreateApiException(
                    HttpStatusCode.BadRequest,
                    """{"status":400,"errors":{"leaderboard":["A leaderboard tournament requires at least one recorded result to be completed."]}}""")));
    }

    [Fact]
    public void LifecycleFailuresShowTheBackendExplanationAndLocalizeAnythingElse()
    {
        var detail = CreateTournamentDetail();

        Assert.Equal(
            "Tournament has to be in progress to be able to complete",
            detail.ResolveTournamentActionError(
                CreateApiException(HttpStatusCode.BadRequest, "\"Tournament has to be in progress to be able to complete\"")));

        Assert.Equal(
            "At least 2 participants required.",
            detail.ResolveTournamentActionError(
                CreateApiException(HttpStatusCode.BadRequest, "\"At least 2 participants required.\"")));

        Assert.Equal(
            "Feature.tournaments.actionFailed",
            detail.ResolveTournamentActionError(
                CreateApiException(HttpStatusCode.BadRequest, string.Empty)));

        Assert.Equal(
            "Feature.tournaments.actionFailed",
            detail.ResolveTournamentActionError(
                CreateApiException(HttpStatusCode.InternalServerError, """{"message":"The tournament service is unavailable."}""")));

        Assert.Equal(
            "Feature.tournaments.actionUnauthorized",
            detail.ResolveTournamentActionError(
                CreateApiException(HttpStatusCode.Unauthorized, "\"You are not authorized.\"")));
    }

    [Fact]
    public async Task LinkedUserSearchNeedsThreeCharactersAndWaitsForTheDebounce()
    {
        var dialog = new TestableRecordLeaderboardAttemptDialog();
        var searchService = CreateSearchService(out var proxy);
        var alice = new GlobalSearchResultDTO
        {
            Type = GlobalSearchResultType.User,
            DisplayLabel = "Alice",
            SupportingText = "Alice",
            UserId = Guid.NewGuid()
        };
        proxy.Handler = (query, _) => Task.FromResult<IReadOnlyList<GlobalSearchResultDTO>>(
            query == "ali" ? [alice] : []);
        SetPrivateProperty(dialog, "UserSearchService", searchService);

        await dialog.InvokeOnRendererAsync(() => dialog.HandleUserQueryChangedAsync(new ChangeEventArgs { Value = "al" }));

        Assert.Empty(proxy.Queries);
        Assert.Empty(GetField<IReadOnlyList<GlobalSearchResultDTO>>(dialog, "_userResults"));
        Assert.False(GetField<bool>(dialog, "_isSearchingUsers"));

        var pending = dialog.InvokeOnRendererAsync(() => dialog.HandleUserQueryChangedAsync(new ChangeEventArgs { Value = "ali" }));
        await Task.Delay(150);

        Assert.Empty(proxy.Queries);

        await pending;

        Assert.Equal(["ali"], proxy.Queries);
        Assert.Equal(alice.UserId, GetField<IReadOnlyList<GlobalSearchResultDTO>>(dialog, "_userResults").Single().UserId);
        Assert.False(GetField<bool>(dialog, "_isSearchingUsers"));
    }

    [Fact]
    public async Task ANewerLinkedUserQueryCancelsTheInFlightSearch()
    {
        var dialog = new TestableRecordLeaderboardAttemptDialog();
        var searchService = CreateSearchService(out var proxy);
        var slowCallStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelledQueries = new List<string>();
        proxy.Handler = async (query, cancellationToken) =>
        {
            if(query == "slow")
            {
                slowCallStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch(OperationCanceledException)
                {
                    cancelledQueries.Add(query);
                    throw;
                }
            }

            return [new GlobalSearchResultDTO
            {
                Type = GlobalSearchResultType.User,
                DisplayLabel = "Fast",
                SupportingText = "Fast",
                UserId = Guid.NewGuid()
            }];
        };
        SetPrivateProperty(dialog, "UserSearchService", searchService);

        var slowSearch = dialog.InvokeOnRendererAsync(() => dialog.HandleUserQueryChangedAsync(new ChangeEventArgs { Value = "slow" }));
        await slowCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await dialog.InvokeOnRendererAsync(() => dialog.HandleUserQueryChangedAsync(new ChangeEventArgs { Value = "fast" }));
        await slowSearch;

        Assert.Equal(["slow", "fast"], proxy.Queries);
        Assert.Equal(["slow"], cancelledQueries);
        Assert.Equal("Fast", GetField<IReadOnlyList<GlobalSearchResultDTO>>(dialog, "_userResults").Single().DisplayLabel);

        dialog.Dispose();
        Assert.False(GetField<bool>(dialog, "_isSearchingUsers"));
    }

    [Fact]
    public async Task ANewValidLinkedUserQueryImmediatelyClearsResultsAndSelection()
    {
        var dialog = new TestableRecordLeaderboardAttemptDialog();
        var searchService = CreateSearchService(out var proxy);
        var oldUserId = Guid.NewGuid();
        var replacementCallStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementResults = new TaskCompletionSource<IReadOnlyList<GlobalSearchResultDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        proxy.Handler = (query, _) =>
        {
            if(query == "alice")
            {
                return Task.FromResult<IReadOnlyList<GlobalSearchResultDTO>>([new GlobalSearchResultDTO
                {
                    Type = GlobalSearchResultType.User,
                    DisplayLabel = "Alice",
                    SupportingText = "Alice",
                    UserId = oldUserId
                }]);
            }

            replacementCallStarted.TrySetResult(true);
            return replacementResults.Task;
        };
        SetPrivateProperty(dialog, "UserSearchService", searchService);

        await dialog.InvokeOnRendererAsync(() => dialog.HandleUserQueryChangedAsync(new ChangeEventArgs { Value = "alice" }));
        SetField(dialog, "_selectedUserId", oldUserId);
        SetField(dialog, "_selectedUserLabel", "Alice");

        var replacementSearch = dialog.InvokeOnRendererAsync(() =>
            dialog.HandleUserQueryChangedAsync(new ChangeEventArgs { Value = "bob" }));
        await Task.Delay(50);

        Assert.Empty(GetField<IReadOnlyList<GlobalSearchResultDTO>>(dialog, "_userResults"));
        Assert.False(GetField<Guid?>(dialog, "_selectedUserId").HasValue);
        Assert.Null(GetField<string?>(dialog, "_selectedUserLabel"));
        Assert.True(GetField<bool>(dialog, "_isSearchingUsers"));

        await replacementCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(GetField<IReadOnlyList<GlobalSearchResultDTO>>(dialog, "_userResults"));

        replacementResults.SetResult([]);
        await replacementSearch;
        dialog.Dispose();
    }

    private static RecordLeaderboardAttemptDialog CreateDialog(LeaderboardRankingMetric rankingMetric)
    {
        var dialog = new RecordLeaderboardAttemptDialog { RankingMetric = rankingMetric };
        SetPrivateProperty(dialog, "Localization", TestLocalizationService.Instance);
        return dialog;
    }

    [Fact]
    public void CreateTournamentFormDefaultsToIndividualParticipation()
    {
        var dialog = CreateAddTournamentDialog();

        var dto = GetField<CreateTournamentDTO>(dialog, "_newTournament");

        Assert.Equal(ParticipationMode.Individual, dto.ParticipationMode);
    }

    [Theory]
    [InlineData(500, """{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"An error occurred while processing your request.","status":500,"traceId":"00-ff7ee017a1914d40d2ea070561fe584b3-afd034ad78951b6a-00"}""")]
    [InlineData(503, """{"status":503,"traceId":"00-abc"}""")]
    [InlineData(500, "<html><body>Internal Server Error</body></html>")]
    [InlineData(400, """{"status":400,"traceId":"00-def"}""")]
    public void CreateTournamentFailureNeverRendersRawApiContent(int statusCode, string content)
    {
        var dialog = CreateAddTournamentDialog();

        var message = dialog.ResolveCreateError(CreateApiException((HttpStatusCode)statusCode, content));

        Assert.Equal("Feature.tournaments.createFailed", message);
        Assert.NotEqual(content, message);
        Assert.DoesNotContain("{", message, StringComparison.Ordinal);
        Assert.DoesNotContain("traceId", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"status":400,"errors":{"Name":["The name is already in use."]}}""", "The name is already in use.")]
    [InlineData("""{"status":400,"detail":"Planned start time must be in the future."}""", "Planned start time must be in the future.")]
    [InlineData("""{"status":400,"message":"The tournament name is already in use."}""", "The tournament name is already in use.")]
    [InlineData("""{"status":400,"errors":{"TeamSize":"Enter a team size between 1 and 50."}}""", "Enter a team size between 1 and 50.")]
    public void CreateTournamentValidationFailureKeepsTheSafeBackendMessage(string content, string expectedMessage)
    {
        var dialog = CreateAddTournamentDialog();

        var message = dialog.ResolveCreateError(CreateApiException(HttpStatusCode.BadRequest, content));

        Assert.Equal(expectedMessage, message);
    }

    [Theory]
    [InlineData("A leaderboard tournament requires a ranking metric.")]
    [InlineData("\"A leaderboard tournament requires a ranking metric.\"")]
    [InlineData("<html><body>Bad Request</body></html>")]
    [InlineData("""{"title":"One or more validation errors occurred."}""")]
    [InlineData("""{"status":400,"traceId":"00-abc"}""")]
    [InlineData("""{"errors":{"Name":[]}}""")]
    [InlineData("""["The name is already in use."]""")]
    public void CreateTournamentUnstructuredFailureFallsBackToTheLocalizedMessage(string content)
    {
        var dialog = CreateAddTournamentDialog();

        var message = dialog.ResolveCreateError(CreateApiException(HttpStatusCode.BadRequest, content));

        Assert.Equal("Feature.tournaments.createFailed", message);
        Assert.NotEqual(content, message);
        Assert.DoesNotContain("{", message, StringComparison.Ordinal);
        Assert.DoesNotContain("traceId", message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateTournamentAuthorizationFailureStaysLocalized()
    {
        var dialog = CreateAddTournamentDialog();

        var message = dialog.ResolveCreateError(
            CreateApiException(HttpStatusCode.Unauthorized, """{"title":"Unauthorized"}"""));

        Assert.Equal("Feature.tournaments.createUnauthorized", message);
    }

    [Theory]
    [InlineData(TournamentStatus.Scheduled, false, "Feature.leaderboard.entryUnavailableScheduled")]
    [InlineData(TournamentStatus.InProgress, true, null)]
    [InlineData(TournamentStatus.Completed, false, "Feature.leaderboard.entryUnavailableClosed")]
    [InlineData(TournamentStatus.Canceled, false, "Feature.leaderboard.entryUnavailableClosed")]
    public void ResultEntryExplainsWhyItIsUnavailable(
        TournamentStatus status,
        bool expectedCanManageResults,
        string? expectedReasonKey)
    {
        var tab = CreateLeaderboardTab(status);

        Assert.Equal(expectedCanManageResults, GetPrivateProperty<bool>(tab, "CanManageResults"));
        Assert.Equal(expectedReasonKey, tab.ResultEntryUnavailableReason);
    }

    private static TournamentDetail CreateTournamentDetail()
    {
        var detail = new TournamentDetail();
        SetPrivateProperty(detail, "Localization", TestLocalizationService.Instance);
        return detail;
    }

    private static AddTournamentDialog CreateAddTournamentDialog()
    {
        var dialog = new AddTournamentDialog();
        SetPrivateProperty(dialog, "Localization", TestLocalizationService.Instance);
        return dialog;
    }

    private static TournamentLeaderboardTab CreateLeaderboardTab(TournamentStatus status)
    {
        var tab = new TournamentLeaderboardTab
        {
            Tournament = new TournamentExtended
            {
                BracketType = BracketType.Leaderboard,
                Status = status
            }
        };
        SetPrivateProperty(tab, "Localization", TestLocalizationService.Instance);
        return tab;
    }

    private static T GetPrivateProperty<T>(object instance, string name)
    {
        var property = FindProperty(instance.GetType(), name)
            ?? throw new InvalidOperationException($"Property '{name}' was not found.");
        return (T)property.GetValue(instance)!;
    }

    private static Type GetNestedEnum(Type declaringType, string enumName) =>
        declaringType.GetNestedType(enumName, BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Nested enum '{enumName}' was not found.");

    private static ITournamentService CreateRecordingTournamentService(out List<string> calls)
    {
        var proxy = DispatchProxy.Create<ITournamentService, RecordingTournamentServiceProxy>();
        var recording = (RecordingTournamentServiceProxy)(object)proxy;
        calls = recording.Calls;
        return proxy;
    }

    private static IUserSearchService CreateSearchService(out RecordingUserSearchServiceProxy proxy)
    {
        var searchService = DispatchProxy.Create<IUserSearchService, RecordingUserSearchServiceProxy>();
        proxy = (RecordingUserSearchServiceProxy)(object)searchService;
        return searchService;
    }

    private static Task InvokePrivateTask(object instance, string methodName, params object?[] arguments)
    {
        var method = FindMethod(instance.GetType(), methodName)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        return (Task)(method.Invoke(instance, arguments)
            ?? throw new InvalidOperationException($"Method '{methodName}' did not return a task."));
    }

    private static void SetPrivateProperty(object instance, string name, object? value)
    {
        var property = FindProperty(instance.GetType(), name)
            ?? throw new InvalidOperationException($"Property '{name}' was not found.");
        property.SetValue(instance, value);
    }

    private static void SetField(object instance, string name, object? value)
    {
        var field = FindField(instance.GetType(), name)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
        field.SetValue(instance, value);
    }

    private static T GetField<T>(object instance, string name)
    {
        var field = FindField(instance.GetType(), name)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
        return (T)field.GetValue(instance)!;
    }

    private static MethodInfo? FindMethod(Type instanceType, string name)
    {
        for(var current = instanceType; current is not null; current = current.BaseType)
        {
            var method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if(method is not null)
                return method;
        }

        return null;
    }

    private static PropertyInfo? FindProperty(Type instanceType, string name)
    {
        for(var current = instanceType; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if(property is not null)
                return property;
        }

        return null;
    }

    private static FieldInfo? FindField(Type instanceType, string name)
    {
        for(var current = instanceType; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if(field is not null)
                return field;
        }

        return null;
    }

    private static ApiException CreateApiException(HttpStatusCode statusCode, string content)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            "https://example.test/v1/lan/tournaments/1a111111-1111-1111-1111-111111111111/lifecycle-state");
        using var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return ApiException.Create(request, HttpMethod.Put, response, new RefitSettings(), innerException: null)
            .GetAwaiter()
            .GetResult();
    }

    private sealed class TestableRecordLeaderboardAttemptDialog : RecordLeaderboardAttemptDialog
    {
        private readonly TestRenderer _renderer = new();

        public TestableRecordLeaderboardAttemptDialog()
        {
            _renderer.Attach(this);
            SetPrivateProperty(this, "Localization", TestLocalizationService.Instance);
        }

        public Task InvokeOnRendererAsync(Func<Task> action) => _renderer.Dispatcher.InvokeAsync(action);
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
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }

    public class RecordingTournamentServiceProxy : DispatchProxy
    {
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod?.Name ?? "<unknown>");
            throw new NotSupportedException($"Unexpected tournament service call: {targetMethod?.Name}");
        }
    }

    public class RecordingUserSearchServiceProxy : DispatchProxy
    {
        public Func<string, CancellationToken, Task<IReadOnlyList<GlobalSearchResultDTO>>> Handler { get; set; } =
            (_, _) => Task.FromResult<IReadOnlyList<GlobalSearchResultDTO>>([]);

        public List<string> Queries { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name != nameof(IUserSearchService.SearchAsync))
                throw new NotSupportedException($"Unexpected user search call: {targetMethod?.Name}");

            var query = (string)args![0]!;
            var cancellationToken = (CancellationToken)args[1]!;
            Queries.Add(query);
            return Handler(query, cancellationToken);
        }
    }
}
