using System.Reflection;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Components.Pages.Teams;
using Mercurius.LAN.Web.Components.Pages.Users;
using Mercurius.LAN.Web.Services;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class PublicProfilePageStateTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("%20")]
    [InlineData("%20%20")]
    public async Task PublicUserProfile_BlankRouteStopsLoadingWithoutRequestingData(string route)
    {
        var (client, service) = CreateUserClient();
        var page = new TestPublicUserProfile();
        SetInjectedService(page, nameof(PublicUserProfile), "UserClient", client);
        SetParameter(page, nameof(PublicUserProfile.Username), route);

        await page.LoadAsync();

        Assert.Empty(service.RequestedUsernames);
        Assert.False(ReadBoolean(page, nameof(PublicUserProfile), "_isLoading"));
        Assert.False(ReadBoolean(page, nameof(PublicUserProfile), "_hasError"));
        Assert.Null(ReadField(page, nameof(PublicUserProfile), "_profile"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("%20")]
    [InlineData("%20%20")]
    public async Task PublicTeamProfile_BlankRouteStopsLoadingWithoutRequestingData(string route)
    {
        var service = new StubTeamService(delayFirstCall: false);
        var page = new TestPublicTeamProfile();
        SetInjectedService(page, nameof(PublicTeamProfile), "TeamService", service);
        SetParameter(page, nameof(PublicTeamProfile.TeamName), route);

        await page.LoadAsync();

        Assert.Empty(service.RequestedTeamNames);
        Assert.False(ReadBoolean(page, nameof(PublicTeamProfile), "_isLoading"));
        Assert.False(ReadBoolean(page, nameof(PublicTeamProfile), "_hasError"));
        Assert.Null(ReadField(page, nameof(PublicTeamProfile), "_team"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicUserProfile_IgnoresSupersededSuccessOrError(bool failFirstCall)
    {
        var (client, service) = CreateUserClient();
        service.DelayFirstProfileCall = true;
        var page = new TestPublicUserProfile();
        SetInjectedService(page, nameof(PublicUserProfile), "UserClient", client);
        SetParameter(page, nameof(PublicUserProfile.Username), "A");

        var firstLoad = page.LoadAsync();
        await service.FirstProfileCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        SetParameter(page, nameof(PublicUserProfile.Username), "B");
        await page.LoadAsync();

        if(failFirstCall)
            service.FirstProfileCallResult.SetException(new InvalidOperationException("stale failure"));
        else
            service.FirstProfileCallResult.SetResult(new UserDTO
            {
                Username = "A",
                Firstname = "Stale",
                Lastname = "Profile"
            });

        await firstLoad;

        Assert.Equal(["A", "B"], service.RequestedUsernames);
        Assert.Equal("B", Assert.IsType<UserDTO>(ReadField(page, nameof(PublicUserProfile), "_profile")).Username);
        Assert.False(ReadBoolean(page, nameof(PublicUserProfile), "_isLoading"));
        Assert.False(ReadBoolean(page, nameof(PublicUserProfile), "_hasError"));
        page.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicTeamProfile_IgnoresSupersededSuccessOrError(bool failFirstCall)
    {
        var service = new StubTeamService(delayFirstCall: true);
        var page = new TestPublicTeamProfile();
        SetInjectedService(page, nameof(PublicTeamProfile), "TeamService", service);
        SetParameter(page, nameof(PublicTeamProfile.TeamName), "A");

        var firstLoad = page.LoadAsync();
        await service.FirstCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        SetParameter(page, nameof(PublicTeamProfile.TeamName), "B");
        await page.LoadAsync();

        if(failFirstCall)
            service.FirstCallResult.SetException(new InvalidOperationException("stale failure"));
        else
            service.FirstCallResult.SetResult(new PublicTeamProfileDTO { TeamName = "A" });

        await firstLoad;

        Assert.Equal(["A", "B"], service.RequestedTeamNames);
        Assert.Null(ReadField(page, nameof(PublicTeamProfile), "_team"));
        Assert.False(ReadBoolean(page, nameof(PublicTeamProfile), "_isLoading"));
        Assert.False(ReadBoolean(page, nameof(PublicTeamProfile), "_hasError"));
        page.Dispose();
    }

    [Fact]
    public async Task PublicUserProfile_IgnoresStaleSummaryAfterRouteChange()
    {
        var (client, service) = CreateUserClient(controlSummaries: true);
        service.DelaySecondSummary = false;
        var page = new TestPublicUserProfile();
        SetInjectedService(page, nameof(PublicUserProfile), "UserClient", client);
        SetParameter(page, nameof(PublicUserProfile.Username), "A");

        var firstLoad = page.LoadAsync();
        await service.FirstSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        SetParameter(page, nameof(PublicUserProfile.Username), "B");
        var secondLoad = page.LoadAsync();
        await service.SecondSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await secondLoad;

        service.FirstSummaryResult.SetResult(CreateSummaries("A summary"));
        await firstLoad;

        var profile = Assert.IsType<UserDTO>(ReadField(page, nameof(PublicUserProfile), "_profile"));
        var summaries = Assert.IsType<PublicProfileMatchSummariesDTO>(ReadField(page, nameof(PublicUserProfile), "_matchSummaries"));
        Assert.Equal("B", profile.Username);
        Assert.Equal("B summary", Assert.Single(summaries.UpcomingMatches).TournamentName);
        Assert.False(ReadBoolean(page, nameof(PublicUserProfile), "_hasMatchSummariesError"));
        page.Dispose();
    }

    [Fact]
    public async Task PublicUserProfile_IgnoresStaleRetryAfterRouteChange()
    {
        var (client, service) = CreateUserClient(controlSummaries: true);
        var page = new TestPublicUserProfile();
        SetInjectedService(page, nameof(PublicUserProfile), "UserClient", client);
        SetParameter(page, nameof(PublicUserProfile.Username), "A");

        var initialLoad = page.LoadAsync();
        await service.FirstSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.FirstSummaryResult.SetException(new InvalidOperationException("initial summary failure"));
        await initialLoad;

        var retry = InvokePrivateTask(page, nameof(PublicUserProfile), "RetryMatchSummariesAsync");
        await service.SecondSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        SetParameter(page, nameof(PublicUserProfile.Username), "B");
        var routeChange = page.LoadAsync();
        await routeChange;

        service.SecondSummaryResult.SetException(new InvalidOperationException("stale retry failure"));
        await retry;

        var profile = Assert.IsType<UserDTO>(ReadField(page, nameof(PublicUserProfile), "_profile"));
        var summaries = Assert.IsType<PublicProfileMatchSummariesDTO>(ReadField(page, nameof(PublicUserProfile), "_matchSummaries"));
        Assert.Equal("B", profile.Username);
        Assert.Equal("B summary", Assert.Single(summaries.UpcomingMatches).TournamentName);
        Assert.False(ReadBoolean(page, nameof(PublicUserProfile), "_hasMatchSummariesError"));
        page.Dispose();
    }

    [Fact]
    public async Task PublicTeamProfile_IgnoresStaleSummaryAfterRouteChange()
    {
        var service = new StubTeamService(
            delayFirstCall: false,
            returnProfiles: true,
            controlSummaries: true)
        {
            DelaySecondSummary = false
        };
        var page = new TestPublicTeamProfile();
        SetInjectedService(page, nameof(PublicTeamProfile), "TeamService", service);
        SetParameter(page, nameof(PublicTeamProfile.TeamName), "A");

        var firstLoad = page.LoadAsync();
        await service.FirstSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        SetParameter(page, nameof(PublicTeamProfile.TeamName), "B");
        var secondLoad = page.LoadAsync();
        await service.SecondSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await secondLoad;

        service.FirstSummaryResult.SetResult(CreateSummaries("A summary"));
        await firstLoad;

        var team = Assert.IsType<PublicTeamProfileDTO>(ReadField(page, nameof(PublicTeamProfile), "_team"));
        var summaries = Assert.IsType<PublicProfileMatchSummariesDTO>(ReadField(page, nameof(PublicTeamProfile), "_matchSummaries"));
        Assert.Equal("B", team.TeamName);
        Assert.Equal("B summary", Assert.Single(summaries.UpcomingMatches).TournamentName);
        Assert.False(ReadBoolean(page, nameof(PublicTeamProfile), "_hasMatchSummariesError"));
        page.Dispose();
    }

    [Fact]
    public async Task PublicTeamProfile_IgnoresStaleRetryAfterRouteChange()
    {
        var service = new StubTeamService(
            delayFirstCall: false,
            returnProfiles: true,
            controlSummaries: true);
        var page = new TestPublicTeamProfile();
        SetInjectedService(page, nameof(PublicTeamProfile), "TeamService", service);
        SetParameter(page, nameof(PublicTeamProfile.TeamName), "A");

        var initialLoad = page.LoadAsync();
        await service.FirstSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.FirstSummaryResult.SetException(new InvalidOperationException("initial summary failure"));
        await initialLoad;

        var retry = InvokePrivateTask(page, nameof(PublicTeamProfile), "RetryMatchSummariesAsync");
        await service.SecondSummaryCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        SetParameter(page, nameof(PublicTeamProfile.TeamName), "B");
        var routeChange = page.LoadAsync();
        await routeChange;

        service.SecondSummaryResult.SetException(new InvalidOperationException("stale retry failure"));
        await retry;

        var team = Assert.IsType<PublicTeamProfileDTO>(ReadField(page, nameof(PublicTeamProfile), "_team"));
        var summaries = Assert.IsType<PublicProfileMatchSummariesDTO>(ReadField(page, nameof(PublicTeamProfile), "_matchSummaries"));
        Assert.Equal("B", team.TeamName);
        Assert.Equal("B summary", Assert.Single(summaries.UpcomingMatches).TournamentName);
        Assert.False(ReadBoolean(page, nameof(PublicTeamProfile), "_hasMatchSummariesError"));
        page.Dispose();
    }

    [Fact]
    public async Task PublicUserProfile_DisposeSuppressesPendingLoad()
    {
        var (client, service) = CreateUserClient();
        service.DelayFirstProfileCall = true;
        var page = new TestPublicUserProfile();
        SetInjectedService(page, nameof(PublicUserProfile), "UserClient", client);
        SetParameter(page, nameof(PublicUserProfile.Username), "A");

        var load = page.LoadAsync();
        await service.FirstProfileCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        page.Dispose();
        service.FirstProfileCallResult.SetException(new InvalidOperationException("disposed failure"));
        await load;

        Assert.True(ReadBoolean(page, nameof(PublicUserProfile), "_isLoading"));
        Assert.False(ReadBoolean(page, nameof(PublicUserProfile), "_hasError"));
        Assert.Null(ReadField(page, nameof(PublicUserProfile), "_profile"));
    }

    [Fact]
    public async Task PublicTeamProfile_DisposeSuppressesPendingLoad()
    {
        var service = new StubTeamService(delayFirstCall: true);
        var page = new TestPublicTeamProfile();
        SetInjectedService(page, nameof(PublicTeamProfile), "TeamService", service);
        SetParameter(page, nameof(PublicTeamProfile.TeamName), "A");

        var load = page.LoadAsync();
        await service.FirstCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        page.Dispose();
        service.FirstCallResult.SetException(new InvalidOperationException("disposed failure"));
        await load;

        Assert.True(ReadBoolean(page, nameof(PublicTeamProfile), "_isLoading"));
        Assert.False(ReadBoolean(page, nameof(PublicTeamProfile), "_hasError"));
        Assert.Null(ReadField(page, nameof(PublicTeamProfile), "_team"));
    }

    private static void SetInjectedService(object page, string pageTypeName, string propertyName, object service)
    {
        var pageType = page.GetType().BaseType ?? throw new InvalidOperationException("Test page base type was not found.");
        if(!string.Equals(pageType.Name, pageTypeName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected test page type '{pageType.Name}'.");

        var property = pageType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Injected property '{propertyName}' was not found.");
        property.SetValue(page, service);
    }

    private static bool ReadBoolean(object page, string pageTypeName, string fieldName) =>
        (bool)(ReadField(page, pageTypeName, fieldName) ?? false);

    private static Task InvokePrivateTask(object page, string pageTypeName, string methodName)
    {
        var pageType = page.GetType().BaseType ?? throw new InvalidOperationException("Test page base type was not found.");
        if(!string.Equals(pageType.Name, pageTypeName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected test page type '{pageType.Name}'.");

        var method = pageType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private method '{methodName}' was not found.");
        return (Task)(method.Invoke(page, null) ?? throw new InvalidOperationException($"Private method '{methodName}' returned no task."));
    }

    private static PublicProfileMatchSummariesDTO CreateSummaries(string tournamentName) =>
        new()
        {
            UpcomingMatches =
            [
                new PublicProfileMatchSummaryDTO
                {
                    TournamentName = tournamentName,
                    MatchId = Guid.NewGuid(),
                    TournamentId = Guid.NewGuid()
                }
            ]
        };

    private static (IUserClient Client, StubUserClientProxy Service) CreateUserClient(bool controlSummaries = false)
    {
        var client = DispatchProxy.Create<IUserClient, StubUserClientProxy>();
        var service = (StubUserClientProxy)(object)client;
        service.ControlSummaries = controlSummaries;
        return (client, service);
    }

    private static void SetParameter(object page, string propertyName, object? value)
    {
        var pageType = page.GetType().BaseType ?? throw new InvalidOperationException("Test page base type was not found.");
        var property = pageType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Component parameter '{propertyName}' was not found.");
        property.SetValue(page, value);
    }

    private static object? ReadField(object page, string pageTypeName, string fieldName)
    {
        var pageType = page.GetType().BaseType ?? throw new InvalidOperationException("Test page base type was not found.");
        if(!string.Equals(pageType.Name, pageTypeName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected test page type '{pageType.Name}'.");

        var field = pageType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Private field '{fieldName}' was not found.");
        return field.GetValue(page);
    }

    private sealed class TestPublicUserProfile : PublicUserProfile
    {
        public Task LoadAsync() => base.OnParametersSetAsync();
    }

    private sealed class TestPublicTeamProfile : PublicTeamProfile
    {
        public Task LoadAsync() => base.OnParametersSetAsync();
    }

    private class StubUserClientProxy : DispatchProxy
    {
        private readonly object _syncRoot = new();
        private readonly List<string> _requestedUsernames = [];
        private int _profileCallCount;
        private int _summaryCallCount;

        public bool DelayFirstProfileCall { get; set; }
        public bool ControlSummaries { get; set; }
        public bool DelaySecondSummary { get; set; } = true;

        public TaskCompletionSource<bool> FirstProfileCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<UserDTO> FirstProfileCallResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> FirstSummaryCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondSummaryCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<PublicProfileMatchSummariesDTO> FirstSummaryResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<PublicProfileMatchSummariesDTO> SecondSummaryResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> RequestedUsernames
        {
            get
            {
                lock(_syncRoot)
                    return _requestedUsernames.ToArray();
            }
        }

        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var username = (string)(args?[0] ?? throw new InvalidOperationException("Username was not supplied."));
            if(targetMethod?.Name == nameof(IUserClient.GetUserByUsernameAsync))
            {
                lock(_syncRoot)
                    _requestedUsernames.Add(username);

                if(Interlocked.Increment(ref _profileCallCount) == 1 && DelayFirstProfileCall)
                {
                    FirstProfileCallStarted.TrySetResult(true);
                    return FirstProfileCallResult.Task;
                }

                return Task.FromResult(new UserDTO { Username = username });
            }

            if(targetMethod?.Name == nameof(IUserClient.GetUserMatchSummariesAsync))
            {
                if(!ControlSummaries)
                    return Task.FromResult(CreateSummaries($"{username} summary"));

                switch(Interlocked.Increment(ref _summaryCallCount))
                {
                    case 1:
                        FirstSummaryCallStarted.TrySetResult(true);
                        return FirstSummaryResult.Task;
                    case 2:
                        SecondSummaryCallStarted.TrySetResult(true);
                        return DelaySecondSummary
                            ? SecondSummaryResult.Task
                            : Task.FromResult(CreateSummaries($"{username} summary"));
                    default:
                        return Task.FromResult(CreateSummaries($"{username} summary"));
                }
            }

            throw new NotSupportedException($"User API method '{targetMethod?.Name}' is not configured for this state test.");
        }
    }

    private sealed class StubTeamService(
        bool delayFirstCall,
        bool returnProfiles = false,
        bool controlSummaries = false) : ITeamService
    {
        private readonly object _syncRoot = new();
        private readonly List<string> _requestedTeamNames = [];
        private int _callCount;
        private int _summaryCallCount;

        public TaskCompletionSource<bool> FirstCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<PublicTeamProfileDTO?> FirstCallResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> FirstSummaryCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> SecondSummaryCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<PublicProfileMatchSummariesDTO?> FirstSummaryResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<PublicProfileMatchSummariesDTO?> SecondSummaryResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool DelaySecondSummary { get; set; } = true;

        public IReadOnlyList<string> RequestedTeamNames
        {
            get
            {
                lock(_syncRoot)
                    return _requestedTeamNames.ToArray();
            }
        }

        public Task<TeamPage> GetTeamsAsync(
            int page = 1,
            int pageSize = TeamPage.DefaultPageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PublicTeamProfileDTO?> GetPublicTeamByNameAsync(
            string teamName,
            CancellationToken cancellationToken = default)
        {
            lock(_syncRoot)
                _requestedTeamNames.Add(teamName);

            if(Interlocked.Increment(ref _callCount) == 1 && delayFirstCall)
            {
                FirstCallStarted.TrySetResult(true);
                return FirstCallResult.Task;
            }

            return returnProfiles
                ? Task.FromResult<PublicTeamProfileDTO?>(new PublicTeamProfileDTO { TeamName = teamName })
                : Task.FromResult<PublicTeamProfileDTO?>(null);
        }

        public Task<PublicProfileMatchSummariesDTO?> GetPublicTeamMatchSummariesAsync(
            string teamName,
            CancellationToken cancellationToken = default)
        {
            if(!controlSummaries)
            {
                return Task.FromException<PublicProfileMatchSummariesDTO?>(
                    new InvalidOperationException("Match summaries must not be requested by this state test."));
            }

            switch(Interlocked.Increment(ref _summaryCallCount))
            {
                case 1:
                    FirstSummaryCallStarted.TrySetResult(true);
                    return FirstSummaryResult.Task;
                case 2:
                    SecondSummaryCallStarted.TrySetResult(true);
                    return DelaySecondSummary
                        ? SecondSummaryResult.Task
                        : Task.FromResult<PublicProfileMatchSummariesDTO?>(CreateSummaries($"{teamName} summary"));
                default:
                    return Task.FromResult<PublicProfileMatchSummariesDTO?>(CreateSummaries($"{teamName} summary"));
            }
        }

        public Task<CurrentUserTeamSummaryDTO> GetCurrentUserTeamSummaryAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Team> CreateTeamAsync(CreateTeamDTO team) => throw new NotSupportedException();

        public Task<TeamInvite> InviteUserAsync(Guid teamId, Guid userId) => throw new NotSupportedException();

        public Task<TeamInvite> CancelInviteAsync(Guid teamId, Guid inviteId) => throw new NotSupportedException();

        public Task<TeamInvite> RespondToInviteAsync(Guid inviteId, bool accept) => throw new NotSupportedException();

        public Task<TeamManagementSummaryDTO> LeaveTeamAsync(Guid teamId) => throw new NotSupportedException();

        public Task<TeamManagementSummaryDTO> RemoveMemberAsync(Guid teamId, Guid userId) => throw new NotSupportedException();

        public Task<TeamManagementSummaryDTO> TransferCaptainAsync(Guid teamId, Guid newCaptainUserId) => throw new NotSupportedException();

        public Task<TeamLogoResponseDTO> UploadLogoAsync(Guid teamId, Stream logoStream, string contentType, string fileName) => throw new NotSupportedException();

        public Task<TeamLogoResponseDTO> RemoveLogoAsync(Guid teamId) => throw new NotSupportedException();

        public Task DeleteTeamAsync(Guid teamId) => throw new NotSupportedException();
    }
}
