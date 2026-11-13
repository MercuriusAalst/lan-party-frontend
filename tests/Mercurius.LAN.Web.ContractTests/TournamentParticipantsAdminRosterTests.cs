using System.Reflection;
using System.Security.Claims;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Components.Pages.Tournaments;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
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
using TeamModel = Mercurius.LAN.Web.Models.Participants.Team;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentParticipantsAdminRosterTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Guid TournamentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TeamId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid CaptainId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid MemberId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task AdministratorTeamCardsAreInteractive()
    {
        var service = CreateTournamentService();
        var tab = CreateTab(service, isAdmin: true);

        await tab.LoadAfterRenderAsync();

        Assert.Single(service.AdminRegistrationRequests);
        Assert.Equal((Total: 1, Interactive: 1), tab.CountParticipantCards());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonAdministratorCardsAreNotInteractive(bool authenticated)
    {
        var service = CreateTournamentService();
        var teamTab = CreateTab(service, isAdmin: false, authenticated: authenticated);
        var soloTab = CreateTab(service, isAdmin: false, authenticated: authenticated, teamTournament: false);

        await teamTab.LoadAfterRenderAsync();
        await soloTab.LoadAfterRenderAsync();

        Assert.Empty(service.AdminRegistrationRequests);
        Assert.Equal((Total: 1, Interactive: 0), teamTab.CountParticipantCards());
        Assert.Equal((Total: 1, Interactive: 0), soloTab.CountParticipantCards());
    }

    [Fact]
    public async Task FailedOrLoadingAdminDataKeepsCardsNonInteractive()
    {
        var failing = CreateTournamentService();
        failing.FailAdminRegistrations = true;
        var failedTab = CreateTab(failing, isAdmin: true);
        await failedTab.LoadAfterRenderAsync();

        var loadingTab = CreateTab(CreateTournamentService(), isAdmin: true);
        await loadingTab.LoadAfterRenderAsync();
        SetField(loadingTab, typeof(TournamentParticipantsTab), "_isLoadingAdmin", true);

        Assert.Equal((Total: 1, Interactive: 0), failedTab.CountParticipantCards());
        Assert.Equal((Total: 1, Interactive: 0), loadingTab.CountParticipantCards());
    }

    [Fact]
    public async Task AdministratorIndividualCardsAreInteractive()
    {
        var tab = CreateTab(CreateTournamentService(), isAdmin: true, teamTournament: false);

        await tab.LoadAfterRenderAsync();

        Assert.Equal((Total: 1, Interactive: 1), tab.CountParticipantCards());
    }

    [Fact]
    public async Task KnownRegistrationsOpenTheRosterWithoutARequest()
    {
        var service = CreateTournamentService();
        var details = CreateDetails(service, knownRegistrations: CreateAdminRegistrations());

        await details.OpenTeamAsync(CreateTeamParticipant());
        var members = GetValue<IReadOnlyList<TournamentRosterMemberDTO>>(details, "RosterMembers");

        Assert.Empty(service.AdminRegistrationRequests);
        Assert.Equal(new[] { "captain-name", "member-name" }, members.Select(member => member.User.Username).ToArray());
        Assert.True(members[0].IsCaptain);
    }

    [Fact]
    public async Task TeamBeyondTheLoadedPageIsLookedUpPageByPage()
    {
        var service = CreateTournamentService();
        service.Pages[1] = Enumerable.Range(0, TournamentAdminParticipantDetails.LookupPageSize)
            .Select(_ => CreateOtherTeamRegistration())
            .ToList();
        service.Pages[2] = CreateAdminRegistrations();
        var details = CreateDetails(service, knownRegistrations: []);

        await details.OpenTeamAsync(CreateTeamParticipant());

        Assert.Equal(new int?[] { 1, 2 }, service.AdminRegistrationRequests.Select(request => request.Page).ToArray());
        Assert.All(service.AdminRegistrationRequests, request => Assert.Equal(TournamentAdminParticipantDetails.LookupPageSize, request.PageSize));
        Assert.Equal(TeamId, GetValue<ParticipantViewModel>(details, "RosterParticipant").Team!.Id);
    }

    [Fact]
    public async Task MissingTeamRegistrationShowsAnErrorAndKeepsTheRosterClosed()
    {
        var service = CreateTournamentService();
        service.Pages[1] = [CreateOtherTeamRegistration()];
        var toasts = CreateToastService();
        var details = CreateDetails(service, toasts: toasts);

        await details.OpenTeamAsync(CreateTeamParticipant());

        Assert.Equal(new int?[] { 1 }, service.AdminRegistrationRequests.Select(request => request.Page).ToArray());
        Assert.Null(GetValue<object>(details, "RosterParticipant"));
        Assert.Equal(new[] { "Feature.tournaments.teamMembersUnavailable" }, toasts.Errors);
    }

    [Fact]
    public async Task DisabledDetailsOpenNothingAndRequestNothing()
    {
        var service = CreateTournamentService();
        var users = CreateUserClient();
        var details = CreateDetails(service, users: users, enabled: false);

        await details.OpenTeamAsync(CreateTeamParticipant());
        await details.OpenPlayerAsync("member-name");

        Assert.Empty(service.AdminRegistrationRequests);
        Assert.Empty(users.RequestedUsernames);
        Assert.Null(GetValue<object>(details, "RosterParticipant"));
        Assert.Null(GetValue<object>(details, "Player"));
    }

    [Fact]
    public async Task DisablingOrRemovingTheTeamClosesTheRoster()
    {
        var details = CreateDetails(CreateTournamentService(), knownRegistrations: CreateAdminRegistrations());
        await details.OpenTeamAsync(CreateTeamParticipant());

        details.Participants = [];
        Assert.Null(GetValue<object>(details, "RosterParticipant"));

        details.Participants = [CreateTeamParticipant()];
        details.Enabled = false;
        InvokeOnParametersSet(details);
        details.Enabled = true;
        InvokeOnParametersSet(details);

        Assert.Null(GetValue<object>(details, "RosterParticipant"));
    }

    [Fact]
    public async Task PlayerDetailsComeFromTheAdminUserEndpoint()
    {
        var users = CreateUserClient();
        var details = CreateDetails(CreateTournamentService(), users: users);

        await details.OpenPlayerAsync("solo-player");
        var player = GetValue<PublicUserDTO>(details, "Player");

        Assert.Equal(new[] { "solo-player" }, users.RequestedUsernames);
        Assert.Equal("Steam-solo-player", player.SteamId);
        Assert.Null(GetValue<Guid?>(details, "_returnTeamId"));
    }

    [Fact]
    public async Task RosterMemberOpensPlayerDetailsAndBackReturnsToTheSameRoster()
    {
        var details = CreateDetails(CreateTournamentService(), knownRegistrations: CreateAdminRegistrations());
        await details.OpenTeamAsync(CreateTeamParticipant());

        await InvokeAsync(details, "OpenPlayerFromRosterAsync", "member-name");

        Assert.Equal("member-name", GetValue<PublicUserDTO>(details, "Player").Username);
        Assert.Null(GetValue<object>(details, "RosterParticipant"));

        await InvokeAsync(details, "ReturnToRoster");

        Assert.Null(GetValue<object>(details, "Player"));
        Assert.Equal(TeamId, GetValue<ParticipantViewModel>(details, "RosterParticipant").Team!.Id);
    }

    [Fact]
    public async Task FailedPlayerLookupKeepsTheRosterOpenAndShowsAnError()
    {
        var users = CreateUserClient();
        users.Fail = true;
        var toasts = CreateToastService();
        var details = CreateDetails(CreateTournamentService(), users: users, toasts: toasts, knownRegistrations: CreateAdminRegistrations());
        await details.OpenTeamAsync(CreateTeamParticipant());

        await InvokeAsync(details, "OpenPlayerFromRosterAsync", "member-name");

        Assert.Null(GetValue<object>(details, "Player"));
        Assert.NotNull(GetValue<object>(details, "RosterParticipant"));
        Assert.Equal(new[] { "Feature.tournaments.playerDetailsUnavailable" }, toasts.Errors);
    }

    [Fact]
    public async Task ClosingTheRosterCancelsAPendingPlayerLookup()
    {
        var users = CreateUserClient();
        users.Pending = new TaskCompletionSource<UserDTO>();
        var details = CreateDetails(CreateTournamentService(), users: users, knownRegistrations: CreateAdminRegistrations());
        await details.OpenTeamAsync(CreateTeamParticipant());

        var lookup = InvokeAsync(details, "OpenPlayerFromRosterAsync", "member-name");
        await InvokeAsync(details, "CloseRoster");
        users.Pending.SetResult(new UserDTO { Username = "member-name" });
        await lookup;

        Assert.Null(GetValue<object>(details, "Player"));
        Assert.Null(GetValue<object>(details, "RosterParticipant"));
    }

    [Fact]
    public void DialogListsUsernamesCaptainAndStatusesWithoutPrivateFields()
    {
        var registration = CreateAdminRegistrations()[0];
        var dialog = CreateDialog(registration.RosterMembers.OrderByDescending(member => member.IsCaptain).ToList());

        var text = RenderText(dialog);

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
        var dialog = CreateDialog(CreateAdminRegistrations()[0].RosterMembers);
        dialog.OnMemberSelected = EventCallback.Factory.Create<string>(new object(), (string _) => { });

        var attributes = RenderAttributes(dialog);

        Assert.DoesNotContain(attributes, attribute => attribute.Value?.StartsWith("/users/", StringComparison.Ordinal) == true);
        Assert.Equal(2, attributes.Count(attribute => attribute == ("aria-haspopup", "dialog")));
        Assert.Contains(("Href", "/teams/Team%20Rocket"), attributes);
    }

    [Fact]
    public void DialogRendersNothingWhenClosed()
    {
        var dialog = new TournamentAdminRosterDialog();
        SetLocalization(dialog);

        Assert.Equal(string.Empty, RenderText(dialog));
    }

    [Fact]
    public void ParticipantComponentKeepsTeamLinkAndCaptainOnlyByDefault()
    {
        var component = CreateParticipantComponent();
        component.CanViewUserDetails = true;
        component.OnUserSelected = EventCallback.Factory.Create<PublicUserDTO>(new object(), (PublicUserDTO _) => { });

        var attributes = RenderAttributes(component);

        Assert.Contains(("Href", "/teams/Team%20Rocket"), attributes);
        Assert.Equal(1, CountMemberButtons(attributes));
    }

    [Fact]
    public void AdministratorMatchParticipantKeepsTeamLinkAndOpensEveryMember()
    {
        var component = CreateParticipantComponent();
        component.CanViewUserDetails = true;
        component.CanSelectAllMembers = true;
        component.OnUserSelected = EventCallback.Factory.Create<PublicUserDTO>(new object(), (PublicUserDTO _) => { });

        var attributes = RenderAttributes(component);

        Assert.Contains(("Href", "/teams/Team%20Rocket"), attributes);
        Assert.Equal(2, CountMemberButtons(attributes));
    }

    [Theory]
    [InlineData(true, new[] { "/users/captain-name", "/users/member-name" })]
    [InlineData(false, new string[0])]
    public void PublicTeamPageLinksMembersToProfilesForAdministratorsOnly(bool isAdmin, string[] expectedLinks)
    {
        var page = new Mercurius.LAN.Web.Components.Pages.Teams.PublicTeamProfile();
        SetLocalization(page);
        SetField(page, page.GetType(), "_isAdmin", isAdmin);
        SetField(page, page.GetType(), "_team", new PublicTeamProfileDTO
        {
            TeamName = "Team Rocket",
            CaptainUsername = "captain-name",
            Members =
            [
                new PublicTeamMemberDTO { Username = "captain-name" },
                new PublicTeamMemberDTO { Username = "member-name" }
            ]
        });

        var userLinks = RenderAttributes(page)
            .Where(attribute => attribute.AttributeName == "href" && attribute.Value?.StartsWith("/users/", StringComparison.Ordinal) == true)
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.Equal(expectedLinks, userLinks);
    }

    private static TournamentAdminParticipantDetails CreateDetails(
        RecordingTournamentServiceProxy service,
        RecordingUserClientProxy? users = null,
        RecordingToastServiceProxy? toasts = null,
        IReadOnlyList<AdminTournamentRegistrationDTO>? knownRegistrations = null,
        bool enabled = true)
    {
        var details = new TournamentAdminParticipantDetails
        {
            TournamentId = TournamentId,
            Enabled = enabled,
            Participants = [CreateTeamParticipant()],
            KnownRegistrations = knownRegistrations
        };
        SetLocalization(details);
        SetProperty(details, "TournamentService", service.Proxy);
        SetProperty(details, "UserClient", (users ?? CreateUserClient()).Proxy);
        SetProperty(details, "ToastService", (toasts ?? CreateToastService()).Proxy);
        InvokeOnParametersSet(details);
        return details;
    }

    private static TournamentAdminRosterDialog CreateDialog(IReadOnlyList<TournamentRosterMemberDTO> members)
    {
        var dialog = new TournamentAdminRosterDialog { Participant = CreateTeamParticipant(), Members = members };
        SetLocalization(dialog);
        return dialog;
    }

    private static ParticipantComponent CreateParticipantComponent()
    {
        var component = new ParticipantComponent
        {
            Participant = new ParticipantViewModel
            {
                Id = TeamId,
                DisplayName = "Team Rocket",
                Team = new TeamModel
                {
                    Id = TeamId,
                    Name = "Team Rocket",
                    CaptainUserId = CaptainId,
                    Members =
                    [
                        new PublicUserDTO { Id = CaptainId, Username = "captain-name" },
                        new PublicUserDTO { Id = MemberId, Username = "member-name" }
                    ]
                }
            }
        };
        SetLocalization(component);
        return component;
    }

    private static int CountMemberButtons(IEnumerable<(string AttributeName, string? Value)> attributes) =>
        attributes.Count(attribute => attribute.AttributeName == "class" &&
            attribute.Value?.Contains("participant-member-link", StringComparison.Ordinal) == true);

    private static ParticipantViewModel CreateTeamParticipant() => new()
    {
        Id = TeamId,
        DisplayName = "Team Rocket",
        Team = new TeamModel { Id = TeamId, Name = "Team Rocket", CaptainUserId = CaptainId }
    };

    private static void InvokeOnParametersSet(TournamentAdminParticipantDetails details) =>
        typeof(TournamentAdminParticipantDetails).GetMethod("OnParametersSet", PrivateInstance)!.Invoke(details, null);

    private static Task InvokeAsync(object instance, string method, params object[] args)
    {
        var result = instance.GetType().GetMethod(method, PrivateInstance)!.Invoke(instance, args);
        return result as Task ?? Task.CompletedTask;
    }

    private static T GetValue<T>(object instance, string name)
    {
        var type = instance.GetType();
        var value = type.GetProperty(name, PrivateInstance)?.GetValue(instance) ??
            type.GetField(name, PrivateInstance)?.GetValue(instance);
        return (T)value!;
    }

    private static void SetLocalization(object component) =>
        component.GetType().GetProperty("Localization", PrivateInstance)!.SetValue(component, TestLocalizationService.Instance);

    private static void SetProperty(object instance, string name, object value) =>
        instance.GetType().GetProperty(name, PrivateInstance)!.SetValue(instance, value);

    private static void SetField(object instance, Type type, string name, object? value) =>
        type.GetField(name, PrivateInstance)!.SetValue(instance, value);

    private static RenderTreeFrame[] RenderFrames(object component)
    {
        var builder = new RenderTreeBuilder();
        component.GetType().GetMethod("BuildRenderTree", PrivateInstance)!.Invoke(component, [builder]);
        var frames = builder.GetFrames();
        return frames.Array.Take(frames.Count).ToArray();
    }

    private static string RenderText(object component) =>
        string.Concat(RenderFrames(component)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Text)
            .Select(frame => frame.TextContent));

    private static List<(string AttributeName, string? Value)> RenderAttributes(object component) =>
        RenderFrames(component)
            .Where(frame => frame.FrameType == RenderTreeFrameType.Attribute)
            .Select(frame => (frame.AttributeName, Value: frame.AttributeValue?.ToString()))
            .ToList();

    private static TestableTournamentParticipantsTab CreateTab(
        RecordingTournamentServiceProxy service,
        bool isAdmin,
        bool authenticated = true,
        bool teamTournament = true)
    {
        var tab = new TestableTournamentParticipantsTab();
        var type = typeof(TournamentParticipantsTab);
        type.GetProperty("Localization", PrivateInstance)!.SetValue(tab, TestLocalizationService.Instance);
        tab.Tournament = teamTournament ? CreateTeamTournament() : CreateIndividualTournament();
        tab.PopupOnly = false;
        tab.RegistrationDialogOpen = false;
        type.GetProperty("TournamentService", PrivateInstance)!.SetValue(tab, service.Proxy);
        type.GetProperty("AuthenticationStateProvider", PrivateInstance)!.SetValue(tab, new FixedAuthenticationStateProvider(CreatePrincipal(isAdmin, authenticated)));
        type.GetProperty("TeamRealtimeService", PrivateInstance)!.SetValue(tab, new NoopTeamRealtimeService());
        type.GetProperty("NotificationService", PrivateInstance)!.SetValue(tab, new NoopNotificationService());
        type.GetProperty("ToastService", PrivateInstance)!.SetValue(tab, CreateToastService().Proxy);
        SetField(tab, type, "_registrationFocusTrap", DispatchProxy.Create<IJSObjectReference, NoopJsObjectReferenceProxy>());
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

    private static AdminTournamentRegistrationDTO CreateOtherTeamRegistration()
    {
        var otherTeamId = Guid.NewGuid();
        return new AdminTournamentRegistrationDTO
        {
            Id = Guid.NewGuid(),
            TournamentId = TournamentId,
            Kind = TournamentRegistrationKind.Team,
            Status = TournamentRegistrationStatus.Active,
            Team = new TeamParticipantDTO { Id = otherTeamId, Name = $"Other {otherTeamId:N}", CaptainUserId = Guid.NewGuid() },
            RosterMembers = []
        };
    }

    private static RecordingTournamentServiceProxy CreateTournamentService()
    {
        var proxy = DispatchProxy.Create<ITournamentService, RecordingTournamentServiceProxy>();
        var recording = (RecordingTournamentServiceProxy)(object)proxy;
        recording.Proxy = proxy;
        return recording;
    }

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
            var frames = RenderFrames(this);
            var total = 0;
            var interactive = 0;
            for(var i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                if(frame.FrameType != RenderTreeFrameType.Component ||
                   frame.ComponentType != typeof(ParticipantCardComponent))
                    continue;

                total++;
                for(var j = i + 1; j < i + frame.ComponentSubtreeLength; j++)
                {
                    if(frames[j].FrameType == RenderTreeFrameType.Attribute &&
                       frames[j].AttributeName == "OnParticipantSelected" &&
                       frames[j].AttributeValue is EventCallback<ParticipantViewModel> { HasDelegate: true })
                        interactive++;
                }
            }

            return (total, interactive);
        }
    }

    private sealed class TestRenderer : Renderer
    {
        public TestRenderer()
            : base(new ServiceCollection()
                .AddSingleton<IJSRuntime, NoopJsRuntime>()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
                .AddSingleton(CreateTournamentService().Proxy)
                .AddSingleton(CreateUserClient().Proxy)
                .AddSingleton(CreateToastService().Proxy)
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

        public Dictionary<int, List<AdminTournamentRegistrationDTO>> Pages { get; } = new() { [1] = CreateAdminRegistrations() };

        public List<(int? Page, int? PageSize)> AdminRegistrationRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITournamentService.GetCurrentUserTournamentRegistrationStateAsync) =>
                    Task.FromResult(new CurrentUserTournamentRegistrationStateDTO { TournamentId = TournamentId }),
                nameof(ITournamentService.CheckIndividualTournamentRegistrationEligibilityAsync) =>
                    Task.FromResult(new EligibilityResponseDTO { Eligible = true }),
                nameof(ITournamentService.GetAdminTournamentRegistrationsAsync) => GetAdminRegistrations((int?)args![1], (int?)args[2]),
                _ => throw new NotSupportedException($"Unexpected tournament service call: {targetMethod?.Name}")
            };
        }

        private Task<List<AdminTournamentRegistrationDTO>> GetAdminRegistrations(int? page, int? pageSize)
        {
            AdminRegistrationRequests.Add((page, pageSize));
            if(FailAdminRegistrations)
                return Task.FromException<List<AdminTournamentRegistrationDTO>>(new InvalidOperationException("admin load failed"));

            return Task.FromResult(Pages.TryGetValue(page ?? 1, out var registrations) ? registrations : []);
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
