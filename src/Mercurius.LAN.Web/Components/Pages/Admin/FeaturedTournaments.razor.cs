using Blazored.Toast.Services;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Refit;
using System.Net;

namespace Mercurius.LAN.Web.Components.Pages.Admin;

public partial class FeaturedTournaments : IAsyncDisposable
{
    private const int SlotCount = 4;
    private const int EligiblePageSize = 50;

    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    private List<Tournament> _eligibleTournaments = [];
    private List<Tournament?> _slots = [null, null, null, null];
    private int? _dragIndex;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _loadError;
    private string? _saveError;
    private bool _disposed;

    private IReadOnlyList<Tournament> _selectedTournaments =>
        _slots.Where(slot => slot is not null).Select(slot => slot!).ToList();

    private bool CanSave =>
        _eligibleTournaments.Count >= SlotCount && _slots.All(slot => slot is not null);

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

    // The editor starts from the arrangement visitors currently see, so recovery done by the
    // backend is visible instead of silently reset. Unknown summaries stay displayable.
    private List<Tournament?> BuildSlots(IReadOnlyList<Tournament> featured)
    {
        var slots = featured
            .Take(SlotCount)
            .Select(tournament => _eligibleTournaments.FirstOrDefault(candidate => candidate.Id == tournament.Id) ?? tournament)
            .Cast<Tournament?>()
            .ToList();

        while(slots.Count < SlotCount)
            slots.Add(null);

        return slots;
    }

    private IEnumerable<Tournament> CandidatesFor(int slotIndex)
    {
        var taken = _slots
            .Where((slot, index) => index != slotIndex && slot is not null)
            .Select(slot => slot!.Id)
            .ToHashSet();

        return _eligibleTournaments.Where(tournament => !taken.Contains(tournament.Id));
    }

    private void SetSlot(int slotIndex, ChangeEventArgs args)
    {
        if(_isSaving)
            return;

        var value = args.Value?.ToString();
        _slots[slotIndex] = Guid.TryParse(value, out var tournamentId)
            ? _eligibleTournaments.FirstOrDefault(tournament => tournament.Id == tournamentId)
            : null;
        _saveError = null;
    }

    private void StartDrag(int slotIndex)
    {
        if(_isSaving)
            return;

        _dragIndex = slotIndex;
    }

    private void ClearDrag() => _dragIndex = null;

    private void DropOn(int slotIndex)
    {
        if(!_isSaving && _dragIndex is int from)
            MoveSlot(from, slotIndex);

        _dragIndex = null;
    }

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
        if(selectedIds.Count != SlotCount || selectedIds.Distinct().Count() != SlotCount)
        {
            _saveError = Localization["Admin.Featured.needFour"];
            return;
        }

        _isSaving = true;
        // Snapshot the submitted arrangement so a late event cannot drop a confirmed id.
        var submittedById = _slots
            .Where(slot => slot is not null)
            .ToDictionary(slot => slot!.Id, slot => slot);
        try
        {
            var confirmed = await TournamentService.UpdateFeaturedTournamentsAsync(selectedIds);
            _slots = confirmed.TournamentIds.Select(id => submittedById.GetValueOrDefault(id)).ToList();
            while(_slots.Count < SlotCount)
                _slots.Add(null);

            ToastService.ShowSuccess(Localization["Admin.Featured.saved"]);
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
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
