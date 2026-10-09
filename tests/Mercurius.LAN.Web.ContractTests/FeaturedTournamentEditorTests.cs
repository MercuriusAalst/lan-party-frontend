using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Pages.Admin;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class FeaturedTournamentEditorTests
{
    [Fact]
    public async Task LoaderPaginatesPastTheFiftyTournamentServerLimit()
    {
        var service = CreateService(out var proxy);
        var eligible = Enumerable.Range(1, 60).Select(index => Tournament(index)).ToList();
        var saved = new[] { eligible[54], eligible[55], eligible[56], eligible[57] };
        proxy.GetFeatured = () => Featured(saved);
        proxy.GetTournamentPage = (page, _) => Task.FromResult(
            eligible.Skip((Math.Max(page ?? 1, 1) - 1) * 50).Take(50).ToList());

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        Assert.Equal(60, Eligible(editor).Count);
        Assert.All(proxy.PageRequests, request => Assert.Equal(50, request.PageSize));
        Assert.Contains(proxy.PageRequests, request => request.Page == 2);
        Assert.Equal(saved.Select(tournament => tournament.Id), SlotIds(editor));
        Assert.All(Slots(editor), slot =>
            Assert.Same(Eligible(editor).Single(candidate => candidate.Id == slot!.Id), slot));
        Assert.True(CanSave(editor));
    }

    [Fact]
    public async Task LoaderContinuesPastOneHundredPagesUntilTheInventoryEnds()
    {
        var service = CreateService(out var proxy);
        var repeatedTournament = Tournament(1);
        var lastTournament = Tournament(2);
        proxy.GetFeatured = () => Featured([]);
        proxy.GetTournamentPage = (page, _) => Task.FromResult(
            page == 102
                ? new List<Tournament> { lastTournament }
                : Enumerable.Repeat(repeatedTournament, 50).ToList());

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        // The harness can drive LoadAsync twice (renderer first-render plus the explicit call), so assert the
        // distinct pages actually requested rather than the raw call count.
        Assert.Equal(102, proxy.PageRequests.Select(request => request.Page).Distinct().Count());
        Assert.Contains(lastTournament, Eligible(editor));
    }

    [Fact]
    public async Task EditorIgnoresCanceledTournamentsAndCannotSaveFewerThanFourEligible()
    {
        var service = CreateService(out var proxy);
        var eligible = new[]
        {
            Tournament(1),
            Tournament(2),
            Tournament(3),
            Tournament(4, TournamentStatus.Canceled)
        };
        proxy.GetFeatured = () => Featured([]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(eligible.ToList());

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        Assert.Equal(3, Eligible(editor).Count);
        Assert.DoesNotContain(Eligible(editor), tournament => tournament.Id == IdOf(4));
        Assert.False(CanSave(editor));

        await SaveAsync(editor);

        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal("Admin.Featured.notEnoughEligible", SaveError(editor));
    }

    [Fact]
    public async Task SavingRequiresFourDistinctFilledSlots()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        SetField(editor, "_slots", new List<Tournament?> { a, b, b, d });
        await SaveAsync(editor);

        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal("Admin.Featured.needFour", SaveError(editor));

        SetField(editor, "_slots", new List<Tournament?> { a, b, c, null });
        await SaveAsync(editor);

        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal("Admin.Featured.needFour", SaveError(editor));
    }

    [Fact]
    public async Task ReorderingPreviewsLocallyAndSaveSendsTheCuratedOrder()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });

        var editor = CreateEditor(service);
        await LoadAsync(editor);
        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }, SlotIds(editor));

        MoveSlot(editor, 0, 1);

        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, SlotIds(editor));
        Assert.Empty(proxy.UpdateRequests);

        await SaveAsync(editor);

        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, Assert.Single(proxy.UpdateRequests));
        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, SlotIds(editor));
    }

    [Fact]
    public void RenderedSlotRowsCarryDistinctIdentityKeysThatSurviveAKeyboardMove()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        proxy.GetFeatured = () => Featured([a, b]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b });

        var editor = CreateEditor(service);
        SetField(editor, "_isLoading", false);
        SetField(editor, "_slots", new List<Tournament?> { a, null, b, null });

        var before = RenderedSlotRowKeys(editor);

        // Every row needs its own non-null key, and the two empty slots must not share one.
        Assert.Equal(4, before.Count);
        Assert.DoesNotContain(before, key => key is null);
        Assert.Equal(4, before.Distinct().Count());
        Assert.Equal(new[] { a.Id, b.Id }, new[] { before[0], before[2] }.Select(key => Guid.Parse(key!.ToString()!)));

        // The key follows the tournament, not the row position, so Blazor moves the row (and the focused
        // move buttons inside it) with the tournament instead of reusing the button at the old position.
        MoveSlot(editor, 0, 1);
        var after = RenderedSlotRowKeys(editor);

        Assert.Equal(before[0], after[1]);
        Assert.Equal(before[2], after[2]);
    }

    [Fact]
    public async Task SavingDisablesTheEditorUntilThePendingSaveCompletes()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        var e = Tournament(5);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d, e });
        var pending = new TaskCompletionSource<FeaturedTournamentIdsDTO>(TaskCreationOptions.RunContinuationsAsynchronously);
        proxy.UpdateFeatured = _ => pending.Task;

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        var saveTask = SaveAsync(editor);
        Assert.True(IsSaving(editor));

        // Late events fired while the PUT is in flight must not disturb the submitted arrangement.
        SetSlot(editor, 0, e.Id);
        MoveSlot(editor, 0, 1);
        StartDrag(editor, 2);
        DropOn(editor, 0);

        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }, SlotIds(editor));

        pending.SetResult(Confirmed(IdOf(1), IdOf(2), IdOf(3), IdOf(4)));
        await saveTask;

        Assert.False(IsSaving(editor));
        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }, SlotIds(editor));
        Assert.Null(SaveError(editor));
    }

    [Fact]
    public async Task SaveResolvesConfirmedIdsFromTheSubmittedSnapshot()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });
        var pending = new TaskCompletionSource<FeaturedTournamentIdsDTO>(TaskCreationOptions.RunContinuationsAsynchronously);
        proxy.UpdateFeatured = _ => pending.Task;

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        var saveTask = SaveAsync(editor);

        // A late mutation that slips past the handlers must not wipe the confirmed mapping.
        SetField(editor, "_slots", new List<Tournament?> { null, null, null, null });

        pending.SetResult(Confirmed(IdOf(1), IdOf(2), IdOf(3), IdOf(4)));
        await saveTask;

        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }, SlotIds(editor));
        Assert.Null(SaveError(editor));
    }

    [Fact]
    public async Task FailedSaveKeepsTheEditedArrangementAndSurfacesARecoverableMessage()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });
        proxy.UpdateFeatured = _ => throw CreateApiException(HttpStatusCode.BadRequest);

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        MoveSlot(editor, 0, 1);
        await SaveAsync(editor);

        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, Assert.Single(proxy.UpdateRequests));
        Assert.Equal("Admin.Featured.rejected", SaveError(editor));
        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, SlotIds(editor));

        proxy.UpdateFeatured = _ => Task.FromResult(Confirmed(IdOf(4), IdOf(3), IdOf(2), IdOf(1)));
        await SaveAsync(editor);

        Assert.Equal(new[] { IdOf(4), IdOf(3), IdOf(2), IdOf(1) }, SlotIds(editor));
        Assert.Null(SaveError(editor));
    }

    [Fact]
    public async Task UnauthorizedSaveLeavesTheSavedArrangementUntouched()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });
        proxy.UpdateFeatured = _ => throw CreateApiException(HttpStatusCode.Forbidden);

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        await SaveAsync(editor);

        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }, Assert.Single(proxy.UpdateRequests));
        Assert.Equal("Admin.Featured.forbidden", SaveError(editor));
        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }, SlotIds(editor));
    }

    [Fact]
    public void EditorIsAdminOnlyAndDisablesItselfWhileSaving()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Admin/FeaturedTournaments.razor");

        Assert.Contains("@attribute [Authorize(Roles = \"admin\")]", markup);
        Assert.Contains("draggable=\"@(!_isSaving)\"", markup);
        Assert.Contains("disabled=\"@_isSaving\"", markup);
        Assert.Contains("slotIndex == 0 || _isSaving", markup);
        Assert.Contains("slotIndex == SlotCount - 1 || _isSaving", markup);
        Assert.Contains("disabled=\"@(!CanSave || _isSaving)\"", markup);
    }

    private static FeaturedTournamentIdsDTO Confirmed(params Guid[] ids) =>
        new() { TournamentIds = ids.ToList() };

    private static FeaturedTournamentsDTO Featured(IEnumerable<Tournament> tournaments)
    {
        var list = tournaments.ToList();
        return new FeaturedTournamentsDTO
        {
            TournamentIds = list.Select(tournament => tournament.Id).ToList(),
            Tournaments = list
        };
    }

    private static Guid IdOf(int index) => new(index, 0, 0, new byte[8]);

    private static Tournament Tournament(int index, TournamentStatus status = TournamentStatus.Scheduled) => new()
    {
        Id = IdOf(index),
        Name = $"Tournament {index}",
        Status = status,
        BracketType = BracketType.SingleElimination,
        Format = TournamentFormat.BestOf1,
        FinalsFormat = TournamentFormat.BestOf1
    };

    private static ITournamentService CreateService(out FeaturedTournamentServiceProxy proxy)
    {
        var service = DispatchProxy.Create<ITournamentService, FeaturedTournamentServiceProxy>();
        proxy = (FeaturedTournamentServiceProxy)(object)service;
        return service;
    }

    private static TestableFeaturedTournaments CreateEditor(ITournamentService service)
    {
        var editor = new TestableFeaturedTournaments();
        SetInjectedProperty(editor, "TournamentService", service);
        SetInjectedProperty(editor, "ToastService", DispatchProxy.Create<IToastService, RecordingToastServiceProxy>());
        SetInjectedProperty(editor, "Localization", TestLocalizationService.Instance);
        return editor;
    }

    private static Task LoadAsync(FeaturedTournaments editor) => InvokeTask(editor, "LoadAsync");

    private static Task SaveAsync(FeaturedTournaments editor) => InvokeTask(editor, "SaveAsync");

    private static void MoveSlot(FeaturedTournaments editor, int from, int to) =>
        Invoke(editor, "MoveSlot", from, to);

    private static void StartDrag(FeaturedTournaments editor, int slotIndex) =>
        Invoke(editor, "StartDrag", slotIndex);

    private static void DropOn(FeaturedTournaments editor, int slotIndex) =>
        Invoke(editor, "DropOn", slotIndex);

    private static void SetSlot(FeaturedTournaments editor, int slotIndex, Guid id) =>
        Invoke(editor, "SetSlot", slotIndex, new ChangeEventArgs { Value = id.ToString() });

    private static Task InvokeTask(FeaturedTournaments editor, string name) =>
        (Task)InstanceMethod(name).Invoke(editor, null)!;

    private static void Invoke(FeaturedTournaments editor, string name, params object?[] arguments) =>
        InstanceMethod(name).Invoke(editor, arguments);

    private static MethodInfo InstanceMethod(string name) =>
        typeof(FeaturedTournaments).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Method '{name}' was not found.");

    private static List<Tournament> Eligible(FeaturedTournaments editor) =>
        (List<Tournament>)ReadField(editor, "_eligibleTournaments")!;

    private static List<Tournament?> Slots(FeaturedTournaments editor) =>
        (List<Tournament?>)ReadField(editor, "_slots")!;

    private static List<Guid> SlotIds(FeaturedTournaments editor) =>
        Slots(editor).Select(slot => slot!.Id).ToList();

    // Runs the Razor-generated BuildRenderTree directly so the diffing keys Blazor would emit for the
    // slot rows can be asserted without spinning up a browser.
    private static List<object?> RenderedSlotRowKeys(FeaturedTournaments editor)
    {
        using var builder = new RenderTreeBuilder();
        InstanceMethod("BuildRenderTree").Invoke(editor, [builder]);

        var frames = builder.GetFrames();
        var keys = new List<object?>();
        for(var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if(frame.FrameType == RenderTreeFrameType.Element && frame.ElementName == "li")
                keys.Add(frame.ElementKey);
        }

        return keys;
    }

    private static bool IsSaving(FeaturedTournaments editor) => (bool)ReadField(editor, "_isSaving")!;

    private static string? SaveError(FeaturedTournaments editor) => (string?)ReadField(editor, "_saveError");

    private static bool CanSave(FeaturedTournaments editor) =>
        (bool)(typeof(FeaturedTournaments)
            .GetProperty("CanSave", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(editor) ?? throw new MissingMemberException(typeof(FeaturedTournaments).FullName, "CanSave"));

    private static object? ReadField(FeaturedTournaments editor, string name) =>
        (typeof(FeaturedTournaments).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(FeaturedTournaments).FullName, name)).GetValue(editor);

    private static void SetField(FeaturedTournaments editor, string name, object? value) =>
        (typeof(FeaturedTournaments).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(FeaturedTournaments).FullName, name)).SetValue(editor, value);

    private static void SetInjectedProperty(FeaturedTournaments editor, string name, object value)
    {
        for(var type = typeof(FeaturedTournaments); type is not null; type = type.BaseType)
        {
            var property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if(property is null)
                continue;

            property.SetValue(editor, value);
            return;
        }

        throw new MissingMemberException(typeof(FeaturedTournaments).FullName, name);
    }

    private static ApiException CreateApiException(HttpStatusCode statusCode)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.test/v1/lan/featured-tournaments");
        using var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        return ApiException.Create(request, HttpMethod.Put, response, new RefitSettings(), innerException: null)
            .GetAwaiter()
            .GetResult();
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null)
        {
            var repositoryPath = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(repositoryPath))
                return File.ReadAllText(repositoryPath);

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");
    }

    private sealed class TestableFeaturedTournaments : FeaturedTournaments
    {
        private readonly TestRenderer _renderer = new();

        public TestableFeaturedTournaments() => _renderer.Attach(this);
    }

    private sealed class TestRenderer : Renderer
    {
        public TestRenderer()
            : base(new ServiceCollection()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
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
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }

    private class RecordingToastServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private class FeaturedTournamentServiceProxy : DispatchProxy
    {
        public Func<FeaturedTournamentsDTO> GetFeatured { get; set; } = () => new FeaturedTournamentsDTO();

        public Func<int?, int?, Task<List<Tournament>>> GetTournamentPage { get; set; } =
            (_, _) => Task.FromResult(new List<Tournament>());

        public Func<IReadOnlyList<Guid>, Task<FeaturedTournamentIdsDTO>> UpdateFeatured { get; set; } =
            ids => Task.FromResult(new FeaturedTournamentIdsDTO { TournamentIds = ids.ToList() });

        public List<(int? Page, int? PageSize)> PageRequests { get; } = [];

        public List<IReadOnlyList<Guid>> UpdateRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch(targetMethod?.Name)
            {
                case nameof(ITournamentService.GetTournamentsAsync):
                    PageRequests.Add(((int?)args![0], (int?)args[1]));
                    return GetTournamentPage((int?)args[0], (int?)args[1]);
                case nameof(ITournamentService.GetFeaturedTournamentsAsync):
                    return Task.FromResult(GetFeatured());
                case nameof(ITournamentService.UpdateFeaturedTournamentsAsync):
                    var ids = (IReadOnlyList<Guid>)args![0]!;
                    UpdateRequests.Add(ids);
                    return UpdateFeatured(ids);
                default:
                    throw new NotSupportedException($"Unexpected tournament service call: {targetMethod?.Name}");
            }
        }
    }
}
