using System.Reflection;
using System.Security.Claims;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentParticipantsAdminRosterTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Guid TournamentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TeamId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid CaptainId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid MemberId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task AdministratorCanOpenTeamRosterFromParticipantCard()
    {
        var service = CreateTournamentService();
        var tab = CreateTab(service, isAdmin: true);

        await tab.LoadAfterRenderAsync();

        Assert.Equal(1, service.AdminRegistrationRequestCount);
        Assert.Equal((Total: 1, Interactive: 1), tab.CountParticipantCards());
        Assert.Null(GetProperty<object>(tab, "AdminRosterDialogMembers"));

        tab.OpenRosterDialogFor(TeamId);
        var members = GetProperty<IReadOnlyList<TournamentRosterMemberDTO>>(tab, "AdminRosterDialogMembers");

        Assert.Equal(new string?[] { "captain-name", "member-name" }, members.Select(member => member.User.Username).ToArray());
        Assert.True(members[0].IsCaptain);
    }

    [Fact]
    public async Task ClosingTheDialogClearsTheSelection()
    {
        var tab = CreateTab(CreateTournamentService(), isAdmin: true);
        await tab.LoadAfterRenderAsync();
        tab.OpenRosterDialogFor(TeamId);

        typeof(TournamentParticipantsTab).GetMethod("CloseAdminRosterDialog", PrivateInstance)!.Invoke(tab, null);

        Assert.Null(GetProperty<object>(tab, "AdminRosterDialogParticipant"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonAdministratorCannotOpenTeamRoster(bool authenticated)
    {
        var service = CreateTournamentService();
        var tab = CreateTab(service, isAdmin: false, authenticated: authenticated);

        await tab.LoadAfterRenderAsync();
        // Even if admin data were somehow present, the non-admin render must not use it.
        GetField<List<AdminTournamentRegistrationDTO>>(tab, "_adminRegistrations").AddRange(CreateAdminRegistrations());
        tab.OpenRosterDialogFor(TeamId);

        Assert.Equal(0, service.AdminRegistrationRequestCount);
        Assert.Equal((Total: 1, Interactive: 0), tab.CountParticipantCards());
        Assert.Null(GetProperty<object>(tab, "AdminRosterDialogMembers"));
    }

    [Fact]
    public async Task FailedAdminLoadKeepsCardsNonInteractive()
    {
        var service = CreateTournamentService();
        service.FailAdminRegistrations = true;
        var tab = CreateTab(service, isAdmin: true);

        await tab.LoadAfterRenderAsync();

        Assert.Equal((Total: 1, Interactive: 0), tab.CountParticipantCards());
    }

    [Fact]
    public async Task ReloadingAdminRegistrationsClosesAnOpenDialog()
    {
        var tab = CreateTab(CreateTournamentService(), isAdmin: true);
        await tab.LoadAfterRenderAsync();
        tab.OpenRosterDialogFor(TeamId);
        Assert.NotNull(GetProperty<object>(tab, "AdminRosterDialogMembers"));

        SetPrivateField(tab, "_isLoadingAdmin", true);

        Assert.Equal((Total: 1, Interactive: 0), tab.CountParticipantCards());
        Assert.Null(GetProperty<object>(tab, "AdminRosterDialogMembers"));
    }

    [Fact]
    public async Task ReloadedAdminRegistrationsDoNotReopenTheRoster()
    {
        var tab = CreateTab(CreateTournamentService(), isAdmin: true);
        await tab.LoadAfterRenderAsync();
        tab.OpenRosterDialogFor(TeamId);

        await tab.ReloadAdminRegistrationsAsync();

        Assert.Equal((Total: 1, Interactive: 1), tab.CountParticipantCards());
        Assert.Null(GetProperty<object>(tab, "AdminRosterDialogParticipant"));
    }

    [Fact]
    public async Task ClosingTheRosterCancelsAPendingPlayerLookup()
    {
        var users = CreateUserClient();
        users.Pending = new TaskCompletionSource<UserDTO>();
        var tab = CreateTab(CreateTournamentService(), isAdmin: true, users: users);
        await tab.LoadAfterRenderAsync();
        tab.OpenRosterDialogFor(TeamId);

        var lookup = tab.OpenPlayerFromRosterAsync("member-name");
        typeof(TournamentParticipantsTab).GetMethod("CloseAdminRosterDialog", PrivateInstance)!.Invoke(tab, null);
        users.Pending.SetResult(new UserDTO { Username = "member-name" });
        await lookup;

        Assert.Null(GetProperty<object>(tab, "AdminPlayerDialogUser"));
        Assert.Null(GetProperty<object>(tab, "AdminRosterDialogParticipant"));
    }

    [Fact]
    public async Task AdministratorOpensPlayerDetailsFromAnIndividualCard()
    {
        var users = CreateUserClient();
        var tab = CreateTab(CreateTournamentService(), isAdmin: true, teamTournament: false, users: users);
        await tab.LoadAfterRenderAsync();

        Assert.Equal((Total: 1, Interactive: 1), tab.CountParticipantCards());

        await tab.OpenPlayerFromCardAsync();
        var shown = GetProperty<PublicUserDTO>(tab, "AdminPlayerDialogUser");

        Assert.Equal(new[] { "solo-player" }, users.RequestedUsernames);
        Assert.Equal("solo-player", shown.Username);
        Assert.Equal("Steam-solo-player", shown.SteamId);
        Assert.Null(GetField<Guid?>(tab, "_adminPlayerReturnTeamId"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonAdministratorCannotOpenPlayerDetails(bool authenticated)
    {
        var users = CreateUserClient();
        var tab = CreateTab(CreateTournamentService(), isAdmin: false, authenticated: authenticated, teamTournament: false, users: users);
        await tab.LoadAfterRenderAsync();

        await tab.OpenPlayerFromCardAsync();

        Assert.Equal((Total: 1, Interactive: 0), tab.CountParticipantCards());
        Assert.Empty(users.RequestedUsernames);
        Assert.Null(GetProperty<object>(tab, "AdminPlayerDialogUser"));
    }

    [Fact]
    public async Task RosterMemberOpensPlayerDetailsAndBackReturnsToTheSameRoster()
    {
        var users = CreateUserClient();
        var tab = CreateTab(CreateTournamentService(), isAdmin: true, users: users);
        await tab.LoadAfterRenderAsync();
        tab.OpenRosterDialogFor(TeamId);

        await tab.OpenPlayerFromRosterAsync("member-name");

        Assert.Equal("member-name", GetProperty<PublicUserDTO>(tab, "AdminPlayerDialogUser").Username);
        Assert.Null(GetProperty<object>(tab, "AdminRosterDialogParticipant"));

        typeof(TournamentParticipantsTab).GetMethod("ReturnToAdminRoster", PrivateInstance)!.Invoke(tab, null);

        Assert.Null(GetProperty<object>(tab, "AdminPlayerDialogUser"));
        Assert.Equal(TeamId, GetProperty<Mercurius.LAN.Web.Components.Shared.ParticipantViewModel>(tab, "AdminRosterDialogParticipant").Team!.Id);
    }

    [Fact]
    public async Task FailedPlayerLookupKeepsTheRosterOpenAndShowsAnError()
    {
        var users = CreateUserClient();
        users.Fail = true;
        var toasts = CreateToastService();
        var tab = CreateTab(CreateTournamentService(), isAdmin: true, users: users, toasts: toasts);
        await tab.LoadAfterRenderAsync();
        tab.OpenRosterDialogFor(TeamId);

        await tab.OpenPlayerFromRosterAsync("member-name");

        Assert.Null(GetProperty<object>(tab, "AdminPlayerDialogUser"));
        Assert.NotNull(GetProperty<object>(tab, "AdminRosterDialogParticipant"));
        Assert.Equal(new[] { "Feature.tournaments.playerDetailsUnavailable" }, toasts.Errors);
    }

    [Fact]
    public void DialogListsUsernamesCaptainAndStatusesWithoutPrivateFields()
    {
        var registration = CreateAdminRegistrations()[0];
        var members = registration.RosterMembers.OrderByDescending(member => member.IsCaptain).ToList();
        var dialog = new TournamentAdminRosterDialog
        {
            Participant = new Mercurius.LAN.Web.Components.Shared.ParticipantViewModel
            {
                Id = TeamId,
                DisplayName = "Team Rocket",
                Team = new Mercurius.LAN.Web.Models.Participants.Team { Id = TeamId, Name = "Team Rocket", CaptainUserId = CaptainId }
            },
            Members = members
        };
        typeof(TournamentAdminRosterDialog).GetProperty("Localization", PrivateInstance)!.SetValue(dialog, TestLocalizationService.Instance);

        var text = RenderText(dialog, typeof(TournamentAdminRosterDialog));

        Assert.Contains("Feature.tournaments.adminTeamMembers", text);
        Assert.Contains("captain-name", text);
        Assert.Contains("member-name", text);
        Assert.Contains("shared.captain", text);
        Assert.Contains("Feature.tournaments.confirmationCaptain", text);
        Assert.Contains("Feature.tournaments.confirmationPending", text);
        Assert.DoesNotContain("Secret", text);
        Assert.DoesNotContain("steam-123", text);
        Assert.True(text.IndexOf("captain-name", StringComparison.Ordinal) < text.IndexOf("member-name", StringComparison.Ordinal));
    }

    [Fact]
    public void DialogOffersPlayerDetailsInsteadOfProfileLinksAndLinksTheTeam()
    {
        var dialog = new TournamentAdminRosterDialog
        {
            Participant = new Mercurius.LAN.Web.Components.Shared.ParticipantViewModel
            {
                Id = TeamId,
                DisplayName = "Team Rocket",
                Team = new Mercurius.LAN.Web.Models.Participants.Team { Id = TeamId, Name = "Team Rocket", CaptainUserId = CaptainId }
            },
            Members = CreateAdminRegistrations()[0].RosterMembers,
            OnMemberSelected = EventCallback.Factory.Create<string>(new object(), (string _) => { })
        };
        typeof(TournamentAdminRosterDialog).GetProperty("Localization", PrivateInstance)!.SetValue(dialog, TestLocalizationService.Instance);

        var builder = new RenderTreeBuilder();
        typeof(TournamentAdminRosterDialog).GetMethod("BuildRenderTree", PrivateInstance)!.Invoke(dialog, [builder]);
        var frames = builder.GetFrames();
        var attributes = frames.Array.Take(frames.Count)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Attribute)
            .Select(frame => (frame.AttributeName, Value: frame.AttributeValue?.ToString()))
            .ToList();

        Assert.DoesNotContain(attributes, attribute => attribute.Value?.StartsWith("/users/", StringComparison.Ordinal) == true);
        Assert.Equal(2, attributes.Count(attribute => attribute == ("aria-haspopup", "dialog")));
        Assert.Contains(("Href", "/teams/Team%20Rocket"), attributes);
    }

    [Theory]
    [InlineData("alice", "/users/alice")]
    [InlineData(" a b/c ", "/users/a%20b%2Fc")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void UserProfileRouteEscapesAndSkipsMissingUsernames(string? username, string? expected) =>
        Assert.Equal(expected, Mercurius.LAN.Web.Components.Shared.ProfileRoutes.User(username));

    [Theory]
    [InlineData("Team Rocket", "/teams/Team%20Rocket")]
    [InlineData(null, null)]
    public void TeamProfileRouteEscapesAndSkipsMissingNames(string? teamName, string? expected) =>
        Assert.Equal(expected, Mercurius.LAN.Web.Components.Shared.ProfileRoutes.Team(teamName));

    [Theory]
    [InlineData(true, new[] { "/users/captain-name", "/users/member-name" })]
    [InlineData(false, new string[0])]
    public void PublicTeamPageLinksMembersToProfilesForAdministratorsOnly(bool isAdmin, string[] expectedLinks)
    {
        var page = new Mercurius.LAN.Web.Components.Pages.Teams.PublicTeamProfile();
        var type = page.GetType();
        type.GetProperty("Localization", PrivateInstance)!.SetValue(page, TestLocalizationService.Instance);
        type.GetField("_isAdmin", PrivateInstance)!.SetValue(page, isAdmin);
        type.GetField("_team", PrivateInstance)!.SetValue(page, new Mercurius.LAN.Web.DTOs.PublicProfiles.PublicTeamProfileDTO
        {
            TeamName = "Team Rocket",
            CaptainUsername = "captain-name",
            Members =
            [
                new Mercurius.LAN.Web.DTOs.PublicProfiles.PublicTeamMemberDTO { Username = "captain-name" },
                new Mercurius.LAN.Web.DTOs.PublicProfiles.PublicTeamMemberDTO { Username = "member-name" }
            ]
        });

        var builder = new RenderTreeBuilder();
        type.GetMethod("BuildRenderTree", PrivateInstance)!.Invoke(page, [builder]);
        var frames = builder.GetFrames();
        var userLinks = frames.Array.Take(frames.Count)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "href")
            .Select(frame => frame.AttributeValue?.ToString())
            .Where(href => href?.StartsWith("/users/", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(expectedLinks, userLinks);
    }

    [Fact]
    public void DialogRendersNothingWhenClosed()
    {
        var dialog = new TournamentAdminRosterDialog();
        typeof(TournamentAdminRosterDialog).GetProperty("Localization", PrivateInstance)!.SetValue(dialog, TestLocalizationService.Instance);

        Assert.Equal(string.Empty, RenderText(dialog, typeof(TournamentAdminRosterDialog)));
    }

    private static string RenderText(object component, Type componentType)
    {
        var builder = new RenderTreeBuilder();
        componentType.GetMethod("BuildRenderTree", PrivateInstance)!.Invoke(component, [builder]);
        var frames = builder.GetFrames();
        return string.Concat(frames.Array.Take(frames.Count)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Text)
            .Select(frame => frame.TextContent));
    }

    private static T GetProperty<T>(object instance, string name) =>
        (T)typeof(TournamentParticipantsTab).GetProperty(name, PrivateInstance)!.GetValue(instance)!;

    private static TestableTournamentParticipantsTab CreateTab(
        RecordingTournamentServiceProxy service,
        bool isAdmin,
        bool authenticated = true,
        bool teamTournament = true,
        RecordingUserClientProxy? users = null,
        RecordingToastServiceProxy? toasts = null)
    {
        var tab = new TestableTournamentParticipantsTab();
        SetPrivateProperty(tab, "Localization", TestLocalizationService.Instance);
        tab.Tournament = teamTournament ? CreateTeamTournament() : CreateIndividualTournament();
        tab.PopupOnly = false;
        tab.RegistrationDialogOpen = false;
        SetPrivateProperty(tab, "TournamentService", service.Proxy);
        SetPrivateProperty(tab, "AuthenticationStateProvider", new FixedAuthenticationStateProvider(CreatePrincipal(isAdmin, authenticated)));
        SetPrivateProperty(tab, "TeamRealtimeService", new NoopTeamRealtimeService());
        SetPrivateProperty(tab, "NotificationService", new NoopNotificationService());
        SetPrivateProperty(tab, "UserClient", (users ?? CreateUserClient()).Proxy);
        SetPrivateProperty(tab, "ToastService", (toasts ?? CreateToastService()).Proxy);
        SetPrivateField(tab, "_registrationFocusTrap", DispatchProxy.Create<IJSObjectReference, NoopJsObjectReferenceProxy>());
        tab.SetParametersForTest();
        return tab;
    }

    private static ClaimsPrincipal CreatePrincipal(bool isAdmin, bool authenticated)
    {
        if(!authenticated)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = new List<Claim> { new(ClaimTypes.Name, "test-user") };
        if(isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "admin"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static TournamentExtended CreateTeamTournament() => new()
    {
        Id = TournamentId,
        Name = "Team tournament",
        Status = TournamentStatus.InProgress,
        ParticipationMode = ParticipationMode.Team,
        TeamSize = 2,
        Registrations =
        [
            new PublicTournamentRegistrationDTO
            {
                Id = Guid.NewGuid(),
                TournamentId = TournamentId,
                Kind = TournamentRegistrationKind.Team,
                Status = TournamentRegistrationStatus.Active,
                Team = new PublicTournamentTeamDTO { Id = TeamId, Name = "Team Rocket", CaptainUserId = CaptainId }
            }
        ]
    };

    private static TournamentExtended CreateIndividualTournament() => new()
    {
        Id = TournamentId,
        Name = "Solo tournament",
        Status = TournamentStatus.InProgress,
        ParticipationMode = ParticipationMode.Individual,
        Registrations =
        [
            new PublicTournamentRegistrationDTO
            {
                Id = Guid.NewGuid(),
                TournamentId = TournamentId,
                Kind = TournamentRegistrationKind.Individual,
                Status = TournamentRegistrationStatus.Active,
                User = new PublicUserDTO { Id = CaptainId, Username = "solo-player" }
            }
        ]
    };

    private static List<AdminTournamentRegistrationDTO> CreateAdminRegistrations() =>
    [
        new AdminTournamentRegistrationDTO
        {
            Id = Guid.NewGuid(),
            TournamentId = TournamentId,
            Kind = TournamentRegistrationKind.Team,
            Status = TournamentRegistrationStatus.Active,
            Team = new TeamParticipantDTO { Id = TeamId, Name = "Team Rocket", CaptainUserId = CaptainId },
            RosterMembers =
            [
                new TournamentRosterMemberDTO
                {
                    Id = Guid.NewGuid(),
                    User = new PublicUserDTO { Id = MemberId, Username = "member-name", Firstname = "Secret", SteamId = "steam-123" },
                    ConfirmationStatus = RosterMemberConfirmationStatus.Pending
                },
                new TournamentRosterMemberDTO
                {
                    Id = Guid.NewGuid(),
                    User = new PublicUserDTO { Id = CaptainId, Username = "captain-name" },
                    IsCaptain = true,
                    ConfirmationStatus = RosterMemberConfirmationStatus.AutoConfirmed
                }
            ]
        }
    ];

    private static RecordingUserClientProxy CreateUserClient()
    {
        var proxy = DispatchProxy.Create<IUserClient, RecordingUserClientProxy>();
        var recording = (RecordingUserClientProxy)(object)proxy;
        recording.Proxy = proxy;
        return recording;
    }

    private static RecordingToastServiceProxy CreateToastService()
    {
        var proxy = DispatchProxy.Create<IToastService, RecordingToastServiceProxy>();
        var recording = (RecordingToastServiceProxy)(object)proxy;
        recording.Proxy = proxy;
        return recording;
    }

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

        public Task LoadAfterRenderAsync() => base.OnAfterRenderAsync(false);

        public (int Total, int Interactive) CountParticipantCards()
        {
            var builder = new RenderTreeBuilder();
            typeof(TournamentParticipantsTab)
                .GetMethod("BuildRenderTree", PrivateInstance)!
                .Invoke(this, [builder]);
            var frames = builder.GetFrames();
            var total = 0;
            var interactive = 0;
            for(var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                if(frame.FrameType != RenderTreeFrameType.Component ||
                   frame.ComponentType != typeof(Mercurius.LAN.Web.Components.Shared.ParticipantCardComponent))
                    continue;

                total++;
                for(var j = i + 1; j < i + frame.ComponentSubtreeLength; j++)
                {
                    if(frames.Array[j].FrameType == RenderTreeFrameType.Attribute &&
                       frames.Array[j].AttributeName == "OnParticipantSelected" &&
                       frames.Array[j].AttributeValue is EventCallback<Mercurius.LAN.Web.Components.Shared.ParticipantViewModel> { HasDelegate: true })
                        interactive++;
                }
            }

            return (total, interactive);
        }

        public Task OpenPlayerFromCardAsync()
        {
            var participants = (List<Mercurius.LAN.Web.Components.Shared.ParticipantViewModel>)typeof(TournamentParticipantsTab)
                .GetField("_participants", PrivateInstance)!.GetValue(this)!;
            return (Task)typeof(TournamentParticipantsTab)
                .GetMethod("OpenAdminPlayerFromCardAsync", PrivateInstance)!
                .Invoke(this, [participants.Single()])!;
        }

        public Task ReloadAdminRegistrationsAsync() =>
            (Task)typeof(TournamentParticipantsTab)
                .GetMethod("LoadAdminRegistrationsAsync", PrivateInstance)!
                .Invoke(this, [TournamentId, (long)typeof(TournamentParticipantsTab).GetField("_requestGeneration", PrivateInstance)!.GetValue(this)!, CancellationToken.None])!;

        public Task OpenPlayerFromRosterAsync(string username) =>
            (Task)typeof(TournamentParticipantsTab)
                .GetMethod("OpenAdminPlayerFromRosterAsync", PrivateInstance)!
                .Invoke(this, [username])!;

        public void OpenRosterDialogFor(Guid teamId)
        {
            var participants = (List<Mercurius.LAN.Web.Components.Shared.ParticipantViewModel>)typeof(TournamentParticipantsTab)
                .GetField("_participants", PrivateInstance)!.GetValue(this)!;
            typeof(TournamentParticipantsTab)
                .GetMethod("OpenAdminRosterDialog", PrivateInstance)!
                .Invoke(this, [participants.Single(participant => participant.Team?.Id == teamId)]);
        }
    }

    private sealed class TestRenderer : Renderer
    {
        public TestRenderer()
            : base(new ServiceCollection()
                .AddSingleton<IJSRuntime, NoopJsRuntime>()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
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
        public bool FailAdminRegistrations { get; set; }
        public int AdminRegistrationRequestCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITournamentService.GetCurrentUserTournamentRegistrationStateAsync) =>
                    Task.FromResult(new CurrentUserTournamentRegistrationStateDTO { TournamentId = TournamentId }),
                nameof(ITournamentService.CheckIndividualTournamentRegistrationEligibilityAsync) =>
                    Task.FromResult(new EligibilityResponseDTO { Eligible = true }),
                nameof(ITournamentService.GetAdminTournamentRegistrationsAsync) => GetAdminRegistrations(),
                _ => throw new NotSupportedException($"Unexpected tournament service call: {targetMethod?.Name}")
            };
        }

        private Task<List<AdminTournamentRegistrationDTO>> GetAdminRegistrations()
        {
            AdminRegistrationRequestCount++;
            return FailAdminRegistrations
                ? Task.FromException<List<AdminTournamentRegistrationDTO>>(new InvalidOperationException("admin load failed"))
                : Task.FromResult(CreateAdminRegistrations());
        }
    }

    private class RecordingUserClientProxy : DispatchProxy
    {
        public IUserClient Proxy { get; set; } = null!;
        public bool Fail { get; set; }
        public TaskCompletionSource<UserDTO>? Pending { get; set; }
        public List<string> RequestedUsernames { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name != nameof(IUserClient.GetUserByUsernameAsync))
                throw new NotSupportedException($"Unexpected user client call: {targetMethod?.Name}");

            var username = (string)args![0]!;
            RequestedUsernames.Add(username);
            if(Pending is not null)
                return Pending.Task;

            return Fail
                ? Task.FromException<UserDTO>(new InvalidOperationException("user lookup failed"))
                : Task.FromResult(new UserDTO { Id = Guid.NewGuid(), Username = username, Firstname = "First", Lastname = "Last", SteamId = $"Steam-{username}" });
        }
    }

    private class RecordingToastServiceProxy : DispatchProxy
    {
        public IToastService Proxy { get; set; } = null!;
        public List<string> Errors { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name == nameof(IToastService.ShowError) && args?.FirstOrDefault() is string message)
                Errors.Add(message);

            return null;
        }
    }

    private sealed class NoopNotificationService : ITeamNotificationService
    {
        public event Func<Task>? Changed { add { } remove { } }
        public event Func<Guid, Task>? RosterDecisionChanged { add { } remove { } }
        public IReadOnlyList<TeamNotificationItem> Notifications => [];
        public int UnreadCount => 0;

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RespondToRosterSelectionAsync(
            string notificationId,
            bool accept,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MarkAllReadAsync() => Task.CompletedTask;

        public Task DismissAsync(string id) => Task.CompletedTask;
    }
}
