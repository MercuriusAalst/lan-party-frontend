using Blazored.Toast.Services;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Refit;
using System.Net;

namespace Mercurius.LAN.Web.Components.Shared;

public partial class FeaturedTournamentEditor : IAsyncDisposable
{
    private const int SlotCount = 4;
    private const int EligiblePageSize = 50;

    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    /// <summary>Reports the confirmed server order so the homepage can adopt it after a save.</summary>
    [Parameter] public EventCallback<IReadOnlyList<Tournament>> OnSaved { get; set; }

    [Parameter] public EventCallback OnCancel { get; set; }

    private List<Tournament> _eligibleTournaments = [];
    private List<Tournament?> _slots = [null, null, null, null];
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _loadError;
    private string? _saveError;
    private bool _disposed;

    private bool IsFull => _slots.All(slot => slot is not null);

    private IReadOnlyList<Tournament> _availableTournaments =>
        _eligibleTournaments.Where(candidate => _slots.All(slot => slot?.Id != candidate.Id)).ToList();

    private bool CanSave => _eligibleTournaments.Count >= SlotCount && SelectionIsEligible;

    // Every slot must reference a still-eligible tournament, so a stale or canceled summary can
    // neither enable the save button nor reach the backend.
    private bool SelectionIsEligible =>
        _slots.All(slot => slot is not null
            && _eligibleTournaments.Exists(candidate => candidate.Id == slot.Id))
        && _slots.Select(slot => slot!.Id).Distinct().Count() == SlotCount;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(firstRender && !_disposed)
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadError = null;
        _saveError = null;

        try
        {
            var featured = await TournamentService.GetFeaturedTournamentsAsync();
            var tournaments = new List<Tournament>();
            for(var page = 1; ; page++)
            {
                var batch = await TournamentService.GetTournamentsAsync(page: page, pageSize: EligiblePageSize);
                tournaments.AddRange(batch);
                if(batch.Count < EligiblePageSize)
                    break;
            }

            if(_disposed)
                return;

            _eligibleTournaments = tournaments
                .Where(tournament => tournament.Status != TournamentStatus.Canceled)
                .ToList();
            _slots = BuildSlots(featured?.Tournaments ?? []);
        }
        catch(Exception)
        {
            if(!_disposed)
            {
                _loadError = Localization["Admin.Featured.loadFailed"];
                ToastService.ShowError(_loadError);
            }
        }
        finally
        {
            if(!_disposed)
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private Task RetryLoadAsync() => LoadAsync();

    // The draft starts from the current arrangement, but a featured id the backend can no longer
    // resolve to an eligible tournament is dropped so it cannot be re-saved as if it were valid.
    private List<Tournament?> BuildSlots(IReadOnlyList<Tournament> featured)
    {
        var slots = featured
            .Take(SlotCount)
            .Select(tournament => _eligibleTournaments.FirstOrDefault(candidate => candidate.Id == tournament.Id))
            .Cast<Tournament?>()
            .ToList();

        while(slots.Count < SlotCount)
            slots.Add(null);

        return slots;
    }

    private void Add(Tournament tournament)
    {
        if(_isSaving)
            return;

        var index = _slots.FindIndex(slot => slot is null);
        if(index < 0)
            return;

        _slots[index] = tournament;
        _saveError = null;
    }

    private void RemoveAt(int index)
    {
        if(_isSaving || index < 0 || index >= _slots.Count)
            return;

        _slots[index] = null;
        _saveError = null;
    }

    private void Move((int From, int To) move) => MoveSlot(move.From, move.To);

    private void MoveSlot(int from, int to)
    {
        if(_isSaving || from == to || from < 0 || to < 0 || from >= _slots.Count || to >= _slots.Count)
            return;

        var slot = _slots[from];
        _slots.RemoveAt(from);
        _slots.Insert(to, slot);
        _saveError = null;
    }

    private async Task SaveAsync()
    {
        if(_isSaving)
            return;

        _saveError = null;

        if(_eligibleTournaments.Count < SlotCount)
        {
            _saveError = Localization.Get("Admin.Featured.notEnoughEligible", _eligibleTournaments.Count);
            return;
        }

        var selectedIds = _slots.Where(slot => slot is not null).Select(slot => slot!.Id).ToList();
        if(!SelectionIsEligible)
        {
            _saveError = Localization["Admin.Featured.needFour"];
            return;
        }

        _isSaving = true;
        // Snapshot the submitted arrangement so a late event cannot drop a confirmed id.
        var submittedById = _slots
            .Where(slot => slot is not null)
            .ToDictionary(slot => slot!.Id, slot => slot);
        IReadOnlyList<Tournament> confirmedOrder = [];
        var saved = false;
        try
        {
            var confirmed = await TournamentService.UpdateFeaturedTournamentsAsync(selectedIds);
            _slots = confirmed.TournamentIds.Select(id => submittedById.GetValueOrDefault(id)).ToList();
            while(_slots.Count < SlotCount)
                _slots.Add(null);

            confirmedOrder = _slots.Where(slot => slot is not null).Select(slot => slot!).ToList();
            ToastService.ShowSuccess(Localization["Admin.Featured.saved"]);
            saved = true;
        }
        catch(ApiException ex) when(ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _saveError = Localization["Admin.Featured.forbidden"];
        }
        catch(ApiException ex) when(ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _saveError = Localization["Admin.Featured.rejected"];
        }
        catch(ApiException)
        {
            _saveError = Localization["Admin.Featured.saveFailed"];
        }
        catch(Exception)
        {
            _saveError = Localization["Admin.Featured.saveFailed"];
        }
        finally
        {
            _isSaving = false;
            if(!_disposed)
                await InvokeAsync(StateHasChanged);
        }

        if(saved && !_disposed)
            await OnSaved.InvokeAsync(confirmedOrder);
    }

    private async Task CancelAsync()
    {
        if(_isSaving)
            return;

        await OnCancel.InvokeAsync();
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
