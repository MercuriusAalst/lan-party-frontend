using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Shared;
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
            Assert.Same(Eligible(editor).Single(candidate => candidate.Id == slot.Id), slot));
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
    public async Task EditorIgnoresCanceledTournamentsAndKeepsTheEligibleDraftSaveable()
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

        // Nothing is selected yet, so the minimum-one copy is the only live reason.
        Assert.False(CanSave(editor));
        Assert.Contains(MinOneText, RenderedText(editor));
        Assert.DoesNotContain(NoEligibleText, RenderedText(editor));

        Add(editor, Eligible(editor)[0]);

        Assert.True(CanSave(editor));
        Assert.DoesNotContain(MinOneText, RenderedText(editor));

        await SaveAsync(editor);

        Assert.Equal(new[] { IdOf(1) }, Assert.Single(proxy.UpdateRequests));
    }

    [Fact]
    public async Task EditorWithNoEligibleTournamentsExplainsTheInventoryAndCannotSave()
    {
        var service = CreateService(out var proxy);
        proxy.GetFeatured = () => Featured([]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament>());

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        Assert.Empty(Eligible(editor));
        Assert.False(CanSave(editor));
        Assert.Contains(MinOneText, RenderedText(editor));
        Assert.Contains(NoEligibleText, RenderedText(editor));

        await SaveAsync(editor);

        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal(MinOneText, SaveError(editor));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task SavingAcceptsEverySelectionCountBetweenOneAndFour(int count)
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

        SetField(editor, "_slots", new List<Tournament> { a, b, c, d }.Take(count).ToList());

        Assert.True(CanSave(editor));

        await SaveAsync(editor);

        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }.Take(count), Assert.Single(proxy.UpdateRequests));
        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }.Take(count), SlotIds(editor));
        Assert.Null(SaveError(editor));
    }

    [Fact]
    public async Task SavingRejectsDuplicateSelectionsAndAnEmptySelection()
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

        SetField(editor, "_slots", new List<Tournament> { a, b, b });
        Assert.False(CanSave(editor));
        await SaveAsync(editor);

        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal("Admin.Featured.rejected", SaveError(editor));

        SetField(editor, "_slots", new List<Tournament>());
        Assert.False(CanSave(editor));
        await SaveAsync(editor);

        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal(MinOneText, SaveError(editor));
    }

    [Fact]
    public async Task UnresolvableFeaturedSummariesAreDroppedAndNeverSentToTheBackend()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var extra = Tournament(7);
        var canceled = Tournament(4, TournamentStatus.Canceled);
        var absent = Tournament(9); // never returned by the public tournament listing
        proxy.GetFeatured = () => Featured([a, b, c, absent, canceled]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, Tournament(5), extra });

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        // The unresolvable featured summaries are dropped instead of being shown as selectable.
        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3) }, SlotIds(editor));
        Assert.True(CanSave(editor));

        // Even a stale summary that slipped into a position is refused before the PUT is issued.
        SetField(editor, "_slots", new List<Tournament> { a, c, canceled });
        Assert.False(CanSave(editor));
        await SaveAsync(editor);
        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal("Admin.Featured.rejected", SaveError(editor));

        SetField(editor, "_slots", new List<Tournament> { absent });
        Assert.False(CanSave(editor));
        await SaveAsync(editor);
        Assert.Empty(proxy.UpdateRequests);
        Assert.Equal("Admin.Featured.rejected", SaveError(editor));
    }

    [Fact]
    public async Task ReorderingPreviewsLocallyAndSaveReportsTheCuratedOrder()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });
        IReadOnlyList<Tournament>? reported = null;

        var editor = CreateEditor(service, onSaved: order => reported = order);
        await LoadAsync(editor);
        Assert.Equal(new[] { IdOf(1), IdOf(2), IdOf(3), IdOf(4) }, SlotIds(editor));

        MoveSlot(editor, 0, 1);

        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, SlotIds(editor));
        Assert.Empty(proxy.UpdateRequests);

        await SaveAsync(editor);

        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, Assert.Single(proxy.UpdateRequests));
        Assert.Equal(new[] { IdOf(2), IdOf(1), IdOf(3), IdOf(4) }, reported!.Select(tournament => tournament.Id));
    }

    [Fact]
    public async Task AddingAppendsAvailableTournamentsAndRemovingCompactsTheDraft()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });

        var editor = CreateEditor(service);
        await LoadAsync(editor);

        // A saved selection of three stays three cards instead of being padded to four positions.
        Assert.Equal(new[] { a, b, c }, Slots(editor));
        Assert.True(CanSave(editor));

        Add(editor, d);

        Assert.Equal(new[] { a, b, c, d }, Slots(editor));
        Assert.True(CanSave(editor));

        // A fifth tournament is refused, and so is a duplicate of an already selected one.
        Add(editor, Tournament(5));
        Add(editor, a);

        Assert.Equal(new[] { a, b, c, d }, Slots(editor));

        RemoveAt(editor, 0);

        // The remaining cards close the gap and the first remaining card leads.
        Assert.Equal(new[] { b, c, d }, Slots(editor));
        Assert.True(CanSave(editor));

        Add(editor, a);

        Assert.Equal(new[] { b, c, d, a }, Slots(editor));
    }

    [Fact]
    public async Task RemovingCardsKeepsSaveAvailableUntilTheDraftIsEmptyAndReplenishingClearsTheHint()
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

        Assert.True(CanSave(editor));
        Assert.DoesNotContain(MinOneText, RenderedText(editor));

        RemoveAt(editor, 0);

        // Three cards are a valid selection, so the hint stays hidden.
        Assert.Equal(new[] { b, c, d }, Slots(editor));
        Assert.True(CanSave(editor));
        Assert.DoesNotContain(MinOneText, RenderedText(editor));

        // Two cards still reorder without an empty position in between.
        RemoveAt(editor, 0);
        Assert.Equal(new[] { c, d }, Slots(editor));
        MoveSlot(editor, 1, 0);
        Assert.Equal(new[] { d, c }, Slots(editor));
        Assert.True(CanSave(editor));

        RemoveAt(editor, 0);
        Assert.Equal(new[] { c }, Slots(editor));
        Assert.True(CanSave(editor));

        // Only the empty draft disables Save, so the reason must be visible without a click.
        RemoveAt(editor, 0);
        Assert.Empty(Slots(editor));
        Assert.False(CanSave(editor));
        Assert.Contains(MinOneText, RenderedText(editor));

        Add(editor, a);

        Assert.True(CanSave(editor));
        Assert.DoesNotContain(MinOneText, RenderedText(editor));
    }

    [Fact]
    public async Task CancelClosesTheDraftWithoutSaving()
    {
        var service = CreateService(out var proxy);
        var a = Tournament(1);
        var b = Tournament(2);
        var c = Tournament(3);
        var d = Tournament(4);
        proxy.GetFeatured = () => Featured([a, b, c, d]);
        proxy.GetTournamentPage = (_, _) => Task.FromResult(new List<Tournament> { a, b, c, d });
        var cancelled = false;

        var editor = CreateEditor(service, onCancel: () => cancelled = true);
        await LoadAsync(editor);

        MoveSlot(editor, 0, 3);
        await CancelAsync(editor);

        Assert.True(cancelled);
        Assert.Empty(proxy.UpdateRequests);
        // The move is local-only: Cancel leaves the edited draft array untouched and simply closes it.
        Assert.Equal(new[] { IdOf(2), IdOf(3), IdOf(4), IdOf(1) }, SlotIds(editor));
    }

    [Fact]
    public void EditingCardsRenderOneKeyPerSelectedTournamentAndStaySafeBelowTwoCards()
    {
        var a = Tournament(1);
        var b = Tournament(2);
        var cards = CreateCards(new List<Tournament> { a, b });

        var before = RenderedPositionKeys(cards);

        Assert.Equal(2, before.Count);
        Assert.Equal(2, before.Distinct().Count());
        // The key is the tournament id itself, so Blazor follows the tournament rather than the row.
        Assert.Equal(new object?[] { a.Id, b.Id }, before);

        // The key follows the tournament, not the row position, so Blazor moves the dragged card (and the
        // focused controls inside it) with the tournament instead of reusing the control at the old position.
        SetInjectedProperty(cards, "Slots", new List<Tournament> { b, a });
        var after = RenderedPositionKeys(cards);

        Assert.Equal(before[0], after[1]);
        Assert.Equal(before[1], after[0]);

        // One card renders as the lead without an error, and an emptied draft renders no cards at all.
        Assert.Equal(new object?[] { a.Id }, RenderedPositionKeys(CreateCards(new List<Tournament> { a })));
        Assert.Empty(RenderedPositionKeys(CreateCards([])));
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
        Add(editor, e);
        RemoveAt(editor, 0);
        MoveSlot(editor, 0, 1);

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
        SetField(editor, "_slots", new List<Tournament>());

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
    public void HomepageExposesTheAdminOnlyInlineEditorAndDropsTheRemovedPage()
    {
        var home = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Home.razor");
        var editor = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentEditor.razor");
        var cards = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentCards.razor");

        Assert.Contains("AuthorizeView Roles=\"admin\"", home);
        Assert.Contains("@onclick=\"StartEditingFeatured\"", home);
        Assert.Contains("<FeaturedTournamentEditor OnSaved=\"ApplyFeaturedOrder\" OnCancel=\"CancelEditingFeatured\" />", home);
        Assert.Contains("if(_isEditingFeatured)", home);

        Assert.Contains("disabled=\"@(!CanSave || _isSaving)\"", editor);
        Assert.Contains("disabled=\"@_isSaving\"", editor);
        Assert.Contains("Slots=\"_slots\"", editor);
        Assert.Contains("OnMoveTournament=\"Move\"", editor);
        Assert.Contains("OnRemoveTournament=\"RemoveAt\"", editor);

        // The draft renders the selected cards, so no empty-position placeholder or copy survives.
        Assert.DoesNotContain("home-tournament-slot--empty", cards);
        Assert.DoesNotContain("emptySlot", cards);
        Assert.DoesNotContain(
            "home-tournament-slot--empty",
            ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/FeaturedTournamentCards.razor.css"));
        Assert.DoesNotContain(
            "emptySlot",
            ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.en-US.json"));
        Assert.DoesNotContain(
            "emptySlot",
            ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/locales/translations.nl-BE.json"));

        // Native HTML5 drag needs the literal draggable keywords and a real drop target; the compact
        // position control is the keyboard and single-pointer alternative.
        Assert.Contains("draggable=\"@draggable\"", cards);
        // Enumerated attribute: the rendered values must be the keywords, never a bool, and the whole
        // draft stops dragging once a save is pending.
        var draggableDraft = CreateCards(new List<Tournament> { Tournament(1) });
        Assert.Equal(new[] { "true" }, RenderedAttributeValues(draggableDraft, "draggable"));
        SetInjectedProperty(draggableDraft, "Disabled", true);
        Assert.Equal(new[] { "false" }, RenderedAttributeValues(draggableDraft, "draggable"));
        Assert.Contains("@ondragstart=", cards);
        Assert.Contains("@ondragover:preventDefault=\"true\"", cards);
        Assert.Contains("@ondrop=", cards);
        Assert.Contains("home-tournament-slot-select", cards);

        Assert.False(RepositoryFileExists("src/Mercurius.LAN.Web/Components/Pages/Admin/FeaturedTournaments.razor"));
        Assert.DoesNotContain("/admin/featured-tournaments", ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor"));
        Assert.DoesNotContain("/admin/featured-tournaments", ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/TournamentsOverview.razor"));
        Assert.DoesNotContain("/admin/featured-tournaments", ReadRepositoryFile("src/Mercurius.LAN.Web/Extensions/LocalReturnUrlHelper.cs"));
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

    private static TestableFeaturedTournamentEditor CreateEditor(
        ITournamentService service,
        Action<IReadOnlyList<Tournament>>? onSaved = null,
        Action? onCancel = null)
    {
        var editor = new TestableFeaturedTournamentEditor();
        SetInjectedProperty(editor, "TournamentService", service);
        SetInjectedProperty(editor, "ToastService", DispatchProxy.Create<IToastService, RecordingToastServiceProxy>());
        SetInjectedProperty(editor, "Localization", TestLocalizationService.Instance);
        if(onSaved is not null)
            SetInjectedProperty(editor, "OnSaved", EventCallback.Factory.Create<IReadOnlyList<Tournament>>(editor, onSaved));
        if(onCancel is not null)
            SetInjectedProperty(editor, "OnCancel", EventCallback.Factory.Create(editor, onCancel));
        return editor;
    }

    private static FeaturedTournamentCards CreateCards(IReadOnlyList<Tournament> slots)
    {
        var cards = new FeaturedTournamentCards();
        SetInjectedProperty(cards, "Configuration", new ConfigurationBuilder().Build());
        SetInjectedProperty(cards, "Localization", TestLocalizationService.Instance);
        SetInjectedProperty(cards, "Slots", slots);
        return cards;
    }

    private static Task LoadAsync(FeaturedTournamentEditor editor) => InvokeTask(editor, "LoadAsync");

    // Save and Cancel invoke the component's own event callbacks, which dispatch back through the
    // component; run them on the renderer's dispatcher exactly like a real UI event would.
    private static Task SaveAsync(FeaturedTournamentEditor editor) =>
        TestableEditor(editor).Dispatcher.InvokeAsync(() => InvokeTask(editor, "SaveAsync"));

    private static Task CancelAsync(FeaturedTournamentEditor editor) =>
        TestableEditor(editor).Dispatcher.InvokeAsync(() => InvokeTask(editor, "CancelAsync"));

    private static TestableFeaturedTournamentEditor TestableEditor(FeaturedTournamentEditor editor) =>
        (TestableFeaturedTournamentEditor)editor;

    private static void MoveSlot(FeaturedTournamentEditor editor, int from, int to) =>
        Invoke(editor, "MoveSlot", from, to);

    private static void Add(FeaturedTournamentEditor editor, Tournament tournament) =>
        Invoke(editor, "Add", tournament);

    private static void RemoveAt(FeaturedTournamentEditor editor, int index) =>
        Invoke(editor, "RemoveAt", index);

    private static Task InvokeTask(FeaturedTournamentEditor editor, string name) =>
        (Task)InstanceMethod(name).Invoke(editor, null)!;

    private static void Invoke(FeaturedTournamentEditor editor, string name, params object?[] arguments) =>
        InstanceMethod(name).Invoke(editor, arguments);

    private static MethodInfo InstanceMethod(string name) =>
        typeof(FeaturedTournamentEditor).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Method '{name}' was not found.");

    private static List<Tournament> Eligible(FeaturedTournamentEditor editor) =>
        (List<Tournament>)ReadField(editor, "_eligibleTournaments")!;

    private static List<Tournament> Slots(FeaturedTournamentEditor editor) =>
        (List<Tournament>)ReadField(editor, "_slots")!;

    private static List<Guid> SlotIds(FeaturedTournamentEditor editor) =>
        Slots(editor).Select(slot => slot.Id).ToList();

    private const string MinOneText = "Admin.Featured.minOne";
    private const string NoEligibleText = "Admin.Featured.noEligible";

    // Runs the editor's Razor-generated BuildRenderTree and returns its literal text so the reactive
    // validation copy can be asserted without a browser.
    private static List<string> RenderedText(FeaturedTournamentEditor editor)
    {
        using var builder = new RenderTreeBuilder();
        var method = typeof(FeaturedTournamentEditor)
                .GetMethod("BuildRenderTree", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildRenderTree was not found.");
        method.Invoke(editor, [builder]);

        var frames = builder.GetFrames();
        var text = new List<string>();
        for(var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if(frame.FrameType == RenderTreeFrameType.Text)
                text.Add(frame.TextContent);
        }

        return text;
    }

    // Runs the Razor-generated BuildRenderTree directly so the diffing keys Blazor would emit for the
    // editable positions can be asserted without spinning up a browser.
    private static List<object?> RenderedPositionKeys(FeaturedTournamentCards cards)
    {
        using var builder = new RenderTreeBuilder();
        var method = cards.GetType().GetMethod("BuildRenderTree", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildRenderTree was not found.");
        method.Invoke(cards, [builder]);

        var frames = builder.GetFrames();
        var keys = new List<object?>();
        for(var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if(frame.FrameType == RenderTreeFrameType.Element && frame.ElementKey is not null)
                keys.Add(frame.ElementKey);
        }

        return keys;
    }

    // Reads a rendered attribute's value from the cards' BuildRenderTree frames, so the enumerated
    // draggable keywords can be asserted as rendered instead of by grepping the markup.
    private static List<string> RenderedAttributeValues(FeaturedTournamentCards cards, string attributeName)
    {
        using var builder = new RenderTreeBuilder();
        var method = cards.GetType().GetMethod("BuildRenderTree", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildRenderTree was not found.");
        method.Invoke(cards, [builder]);

        var frames = builder.GetFrames();
        var values = new List<string>();
        for(var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if(frame.FrameType == RenderTreeFrameType.Attribute
                && frame.AttributeName == attributeName
                && frame.AttributeValue is not null)
            {
                values.Add(frame.AttributeValue.ToString()!);
            }
        }

        return values;
    }

    private static bool IsSaving(FeaturedTournamentEditor editor) => (bool)ReadField(editor, "_isSaving")!;

    private static string? SaveError(FeaturedTournamentEditor editor) => (string?)ReadField(editor, "_saveError");

    private static bool CanSave(FeaturedTournamentEditor editor) =>
        (bool)(typeof(FeaturedTournamentEditor)
            .GetProperty("CanSave", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(editor) ?? throw new MissingMemberException(typeof(FeaturedTournamentEditor).FullName, "CanSave"));

    private static object? ReadField(FeaturedTournamentEditor editor, string name) =>
        (typeof(FeaturedTournamentEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(FeaturedTournamentEditor).FullName, name)).GetValue(editor);

    private static void SetField(FeaturedTournamentEditor editor, string name, object? value) =>
        (typeof(FeaturedTournamentEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(FeaturedTournamentEditor).FullName, name)).SetValue(editor, value);

    private static void SetInjectedProperty(object component, string name, object value)
    {
        for(var type = component.GetType(); type is not null; type = type.BaseType)
        {
            var property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if(property is null)
                continue;

            property.SetValue(component, value);
            return;
        }

        throw new MissingMemberException(component.GetType().FullName, name);
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

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(FindRepositoryFile(relativePath));

    private static bool RepositoryFileExists(string relativePath) =>
        TryFindRepositoryFile(relativePath) is not null;

    private static string FindRepositoryFile(string relativePath) =>
        TryFindRepositoryFile(relativePath)
        ?? throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");

    private static string? TryFindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null)
        {
            var repositoryPath = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(repositoryPath))
                return repositoryPath;

            directory = directory.Parent;
        }

        return null;
    }

    private sealed class TestableFeaturedTournamentEditor : FeaturedTournamentEditor
    {
        private readonly TestRenderer _renderer = new();

        public TestableFeaturedTournamentEditor() => _renderer.Attach(this);

        public Dispatcher Dispatcher => _renderer.Dispatcher;
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
