using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR.Client;

namespace Mercurius.LAN.Web.Services;

public interface ITeamRealtimeService : IAsyncDisposable
{
    event Func<Task>? TeamStateInvalidated;
    bool IsConnected { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task JoinTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default);
}

public sealed class TeamRealtimeService : ITeamRealtimeService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SemaphoreSlim _connectionStartGate = new(1, 1);
    private HubConnection? _connection;
    private Task? _connectionStartTask;

    public TeamRealtimeService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public event Func<Task>? TeamStateInvalidated;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Task startTask;
        await _connectionStartGate.WaitAsync(cancellationToken);
        try
        {
            if(_connection is { State: HubConnectionState.Connected or HubConnectionState.Reconnecting })
                return;

            _connection ??= BuildConnection();
            if(_connection.State != HubConnectionState.Connecting ||
               _connectionStartTask is null ||
               _connectionStartTask.IsCompleted)
            {
                _connectionStartTask = _connection.StartAsync();
            }

            startTask = _connectionStartTask;
        }
        finally
        {
            _connectionStartGate.Release();
        }

        await startTask.WaitAsync(cancellationToken);
    }

    public async Task JoinTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken);
        if(_connection?.State != HubConnectionState.Connected)
            return;

        foreach(var teamId in teamIds.Distinct())
        {
            await _connection.InvokeAsync("JoinTeam", teamId, cancellationToken);
        }
    }

    private HubConnection BuildConnection()
    {
        var configuredBaseAddress = _configuration.GetValue<string>("MercuriusAPI:BaseAddress") ?? string.Empty;
        var hubUrl = $"{DependencyExtensions.BuildApiBaseAddress(configuredBaseAddress)}v1/lan/team-events";

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var httpContext = _httpContextAccessor.HttpContext;
                    return httpContext == null ? null : await httpContext.GetTokenAsync("access_token");
                };
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<TeamInviteChangedEvent>("TeamInviteChanged", _ => NotifyInvalidatedAsync());
        connection.On<TeamMembershipChangedEvent>("TeamMembershipChanged", _ => NotifyInvalidatedAsync());
        connection.On<TeamCaptainTransferredEvent>("TeamCaptainTransferred", _ => NotifyInvalidatedAsync());
        connection.On<TournamentRosterConfirmationChangedEvent>("TournamentRosterConfirmationChanged", _ => NotifyInvalidatedAsync());
        connection.Reconnected += _ => NotifyInvalidatedAsync();

        return connection;
    }

    private async Task NotifyInvalidatedAsync()
    {
        var handler = TeamStateInvalidated;
        if(handler != null)
            await handler();
    }

    public async ValueTask DisposeAsync()
    {
        if(_connection != null)
            await _connection.DisposeAsync();
    }
}

public sealed class NoopTeamRealtimeService : ITeamRealtimeService
{
    public event Func<Task>? TeamStateInvalidated;
    public bool IsConnected => false;
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
