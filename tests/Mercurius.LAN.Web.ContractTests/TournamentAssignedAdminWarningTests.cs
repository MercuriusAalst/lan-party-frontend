using System.Reflection;
using System.Security.Claims;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentAssignedAdminWarningTests
{
    private static readonly Guid ContactId = Guid.Parse("71111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherAdminId = Guid.Parse("72222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task AdminSeesTheWarningWhenTheContactIsNotInTheAdminList()
    {
        var service = new AdminListService(() => Task.FromResult(new List<PublicUserDTO> { Admin(OtherAdminId) }));
        var tab = CreateTab(service, Tournament(ContactId), isAdmin: true);

        await tab.RunParametersAsync();

        Assert.True(tab.ShowWarning);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task AdminSeesNoWarningWhenTheContactIsStillAnAdmin()
    {
        var service = new AdminListService(() => Task.FromResult(new List<PublicUserDTO> { Admin(OtherAdminId), Admin(ContactId) }));
        var tab = CreateTab(service, Tournament(ContactId), isAdmin: true);

        await tab.RunParametersAsync();

        Assert.False(tab.ShowWarning);
        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task NonAdminsAndTheAnonymousPublicNeverGetTheWarningOrLoadTheAdminList()
    {
        var service = new AdminListService(() => Task.FromResult(new List<PublicUserDTO> { Admin(OtherAdminId) }));
        var member = CreateTab(service, Tournament(ContactId), isAdmin: false);
        var anonymous = CreateTab(service, Tournament(ContactId), isAdmin: false, authenticated: false);
        var withoutAuthState = CreateTab(service, Tournament(ContactId), isAdmin: false, withAuthState: false);

        await member.RunParametersAsync();
        await anonymous.RunParametersAsync();
        await withoutAuthState.RunParametersAsync();

        Assert.False(member.ShowWarning);
        Assert.False(anonymous.ShowWarning);
        Assert.False(withoutAuthState.ShowWarning);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task NoWarningWhileTheAdminListIsStillLoading()
    {
        var pending = new TaskCompletionSource<List<PublicUserDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new AdminListService(() => pending.Task);
        var tab = CreateTab(service, Tournament(ContactId), isAdmin: true);

        var evaluation = tab.RunParametersAsync();

        Assert.False(evaluation.IsCompleted);
        Assert.False(tab.ShowWarning);
        Assert.Equal(1, service.Calls);

        pending.SetResult([Admin(OtherAdminId)]);
        await evaluation;
        Assert.True(tab.ShowWarning);
    }

    [Fact]
    public async Task NoWarningWhenTheAdminListFailsToLoadOrIsEmpty()
    {
        var failing = new AdminListService(() => Task.FromException<List<PublicUserDTO>>(new HttpRequestException("offline")));
        var empty = new AdminListService(() => Task.FromResult(new List<PublicUserDTO>()));
        var failingTab = CreateTab(failing, Tournament(ContactId), isAdmin: true);
        var emptyTab = CreateTab(empty, Tournament(ContactId), isAdmin: true);

        await failingTab.RunParametersAsync();
        await emptyTab.RunParametersAsync();

        Assert.False(failingTab.ShowWarning);
        Assert.False(emptyTab.ShowWarning);
        Assert.Equal(1, failing.Calls);
        Assert.Equal(1, empty.Calls);
    }

    [Fact]
    public async Task NoWarningForATournamentWithoutAContactOrAFinishedTournament()
    {
        var service = new AdminListService(() => Task.FromResult(new List<PublicUserDTO> { Admin(OtherAdminId) }));
        var withoutContact = CreateTab(service, Tournament(null), isAdmin: true);
        var completed = CreateTab(service, Tournament(ContactId, TournamentStatus.Completed), isAdmin: true);
        var inProgress = CreateTab(service, Tournament(ContactId, TournamentStatus.InProgress), isAdmin: true);

        await withoutContact.RunParametersAsync();
        await completed.RunParametersAsync();
        await inProgress.RunParametersAsync();

        Assert.False(withoutContact.ShowWarning);
        Assert.False(completed.ShowWarning);
        Assert.True(inProgress.ShowWarning);
    }

    [Fact]
    public async Task ScheduledAsksForAReplacementWhileInProgressOnlyExplainsTheEscalation()
    {
        var service = new AdminListService(() => Task.FromResult(new List<PublicUserDTO> { Admin(OtherAdminId) }));
        var scheduled = CreateTab(service, Tournament(ContactId), isAdmin: true);
        var inProgress = CreateTab(service, Tournament(ContactId, TournamentStatus.InProgress), isAdmin: true);

        await scheduled.RunParametersAsync();
        await inProgress.RunParametersAsync();

        Assert.Equal("Feature.tournaments.assignedAdminRevokedWarning", scheduled.WarningKey);
        Assert.Equal("Feature.tournaments.assignedAdminRevokedWarningInProgress", inProgress.WarningKey);

        var english = ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json");
        var dutch = ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.nl-BE.json");
        Assert.Contains("Feature.tournaments.assignedAdminRevokedWarningInProgress", english);
        Assert.Contains("Feature.tournaments.assignedAdminRevokedWarningInProgress", dutch);
    }

    [Fact]
    public void ComparisonUsesTheAdminIdentity()
    {
        var contact = Admin(ContactId);

        Assert.Equal(
            TournamentOverviewTab.AssignedAdminStatus.Valid,
            TournamentOverviewTab.EvaluateAssignedAdmin(contact, [Admin(ContactId)]));
        Assert.Equal(
            TournamentOverviewTab.AssignedAdminStatus.Revoked,
            TournamentOverviewTab.EvaluateAssignedAdmin(contact, [Admin(OtherAdminId)]));
        Assert.Equal(
            TournamentOverviewTab.AssignedAdminStatus.Unknown,
            TournamentOverviewTab.EvaluateAssignedAdmin(contact, []));
        Assert.Equal(
            TournamentOverviewTab.AssignedAdminStatus.Unknown,
            TournamentOverviewTab.EvaluateAssignedAdmin(contact, null));
    }

    [Fact]
    public void WarningMarkupLivesOnlyInsideTheAdminGatedViewMode()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentOverviewTab.razor");

        var warning = markup.IndexOf("assignedAdminRevokedWarning", StringComparison.Ordinal);
        var gate = markup.LastIndexOf("<AuthorizeView Roles=\"admin\">", warning, StringComparison.Ordinal);
        var gateEnd = markup.IndexOf("</AuthorizeView>", warning, StringComparison.Ordinal);
        Assert.True(warning > 0);
        Assert.True(gate >= 0 && gateEnd > warning, "The warning must sit inside an admin-only AuthorizeView.");
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(markup, "id=\"assignedAdminRevokedWarning\""));
        Assert.Contains("ShowAssignedAdminRevokedWarning", markup);
    }

    private static PublicUserDTO Admin(Guid id) => new() { Id = id, Username = $"admin-{id.ToString()[..4]}", DisplayName = "Admin" };

    private static TournamentExtended Tournament(Guid? contactId, TournamentStatus status = TournamentStatus.Scheduled) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Warning Cup",
        Status = status,
        BracketType = BracketType.SingleElimination,
        ParticipationMode = ParticipationMode.Individual,
        ContactAdmin = contactId is { } id ? Admin(id) : null
    };

    private static TestTab CreateTab(
        AdminListService service,
        TournamentExtended tournament,
        bool isAdmin,
        bool authenticated = true,
        bool withAuthState = true)
    {
        var tournamentService = DispatchProxy.Create<ITournamentService, ServiceProxy>();
        ((ServiceProxy)(object)tournamentService).Handler = (method, _) => method.Name == nameof(ITournamentService.GetAllAdminUsersAsync)
            ? service.GetAsync()
            : throw new NotSupportedException(method.Name);

        var tab = new TestTab { Tournament = tournament };
        SetProperty(tab, "TournamentService", tournamentService);
        if (withAuthState)
        {
            var identity = authenticated
                ? new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-1"), .. (isAdmin ? new[] { new Claim(ClaimTypes.Role, "admin") } : [])],
                    authenticationType: "test")
                : new ClaimsIdentity();
            SetProperty(tab, "AuthenticationStateTask", Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
        }

        return tab;
    }

    private static void SetProperty(object instance, string name, object? value)
    {
        var property = typeof(TournamentOverviewTab).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(TournamentOverviewTab).FullName, name);
        property.SetValue(instance, value);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(path))
                return File.ReadAllText(path);

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");
    }

    private sealed class TestTab : TournamentOverviewTab
    {
        public bool ShowWarning => ShowAssignedAdminRevokedWarning;

        public string WarningKey => AssignedAdminRevokedWarningKey;

        public Task RunParametersAsync() => OnParametersSetAsync();
    }

    private sealed class AdminListService(Func<Task<List<PublicUserDTO>>> load)
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<List<PublicUserDTO>> GetAsync()
        {
            Interlocked.Increment(ref _calls);
            return load();
        }
    }

    private class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = (_, _) => null;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Missing proxy method."), args);
    }
}
