using Blazored.Toast.Services;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Mercurius.LAN.Web.Components.Pages;

public partial class Home : IAsyncDisposable
{
    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private ISponsorService SponsorService { get; set; } = null!;
    [Inject] private IOptions<LanEventOptions> EventOptions { get; set; } = null!;

    [PersistentState] public List<Tournament>? PersistedFeaturedTournaments { get; set; }

    [PersistentState] public bool PersistedFeaturedLoadFailed { get; set; }

    [PersistentState] public List<Sponsor>? PersistedSponsors { get; set; }

    [PersistentState] public bool PersistedSponsorsLoadFailed { get; set; }

    private List<Tournament> _featuredTournaments = [];
    private List<Sponsor> _sponsors = [];
    private bool _isTournamentsLoading = true;
    private bool _isSponsorsLoading = true;
    private string? _tournamentsError;
    private string? _sponsorsError;
    private long _loadVersion;
    private long _featuredLoadVersion;
    private bool _disposed;
    private bool _isEditingFeatured;

    private IReadOnlyList<Tournament> FeaturedTournaments => _featuredTournaments;
    private IReadOnlyList<Tournament> HeroTournaments => _featuredTournaments.Take(3).ToList();

    private string EventWindow => EventOptions.Value.EventWindow;

    private string HeroLocation => $"{EventOptions.Value.VenueName}, {EventOptions.Value.Address}";

    protected override async Task OnInitializedAsync()
    {
        var loadVersion = ++_loadVersion;

        if(PersistedSponsors is not null)
        {
            _sponsors = PersistedSponsors;
            _isSponsorsLoading = false;
            if(PersistedSponsorsLoadFailed)
                _sponsorsError = Localization["General.Home.SponsorsUnavailable"];
        }
        else
        {
            _ = LoadSponsorsAsync(loadVersion);
        }

        if(PersistedFeaturedTournaments is not null)
        {
            _featuredTournaments = PersistedFeaturedTournaments;
            _isTournamentsLoading = false;
            if(PersistedFeaturedLoadFailed)
                _tournamentsError = Localization["General.Home.LoadError"];

            return;
        }

        await LoadTournamentsAsync(loadVersion, renderAfterLoad: false);
    }

    private async Task LoadHomeDataAsync()
    {
        if(_disposed)
            return;

        var loadVersion = ++_loadVersion;
        _isTournamentsLoading = true;
        _isSponsorsLoading = true;
        _tournamentsError = null;
        _sponsorsError = null;
        PersistedFeaturedLoadFailed = false;
        PersistedSponsorsLoadFailed = false;

        await RenderIfCurrentAsync(loadVersion);
        _ = LoadTournamentsAsync(loadVersion);
        _ = LoadSponsorsAsync(loadVersion);
    }

    private async Task LoadTournamentsAsync(long loadVersion, bool renderAfterLoad = true)
    {
        // The featured read keeps its own generation so a confirmed save can retire a response that is
        // still in flight without touching the sponsor read.
        var featuredLoadVersion = ++_featuredLoadVersion;

        try
        {
            var featured = await TournamentService.GetFeaturedTournamentsAsync();
            if(IsCurrentFeaturedLoad(featuredLoadVersion))
            {
                _featuredTournaments = featured?.Tournaments ?? [];
                PersistedFeaturedTournaments = _featuredTournaments;
                PersistedFeaturedLoadFailed = false;
            }
        }
        catch(Exception)
        {
            if(!IsCurrentFeaturedLoad(featuredLoadVersion))
                return;

            _featuredTournaments = [];
            _tournamentsError = Localization["General.Home.LoadError"];
            ToastService.ShowError(Localization["General.Home.LoadToast"]);
            PersistedFeaturedTournaments = [];
            PersistedFeaturedLoadFailed = true;
        }
        finally
        {
            if(IsCurrentFeaturedLoad(featuredLoadVersion))
            {
                _isTournamentsLoading = false;
                if(renderAfterLoad)
                    await RenderIfCurrentAsync(loadVersion);
            }
        }
    }

    private async Task LoadSponsorsAsync(long loadVersion)
    {
        try
        {
            var sponsors = (await SponsorService.GetSponsorsAsync())
                .OrderBy(sponsor => sponsor.SponsorTier.GetDisplayOrder())
                .ThenBy(sponsor => sponsor.Name)
                .ToList();
            if(IsCurrentLoad(loadVersion))
            {
                _sponsors = sponsors;
                PersistedSponsors = sponsors;
                PersistedSponsorsLoadFailed = false;
            }
        }
        catch(Exception)
        {
            if(!IsCurrentLoad(loadVersion))
                return;

            _sponsors = [];
            _sponsorsError = Localization["General.Home.SponsorsUnavailable"];
            PersistedSponsors = [];
            PersistedSponsorsLoadFailed = true;
        }
        finally
        {
            if(IsCurrentLoad(loadVersion))
            {
                _isSponsorsLoading = false;
                await RenderIfCurrentAsync(loadVersion);
            }
        }
    }

    private Task RetryLoadAsync() => LoadHomeDataAsync();

    private bool IsCurrentLoad(long loadVersion) => !_disposed && loadVersion == _loadVersion;

    private bool IsCurrentFeaturedLoad(long featuredLoadVersion) =>
        !_disposed && featuredLoadVersion == _featuredLoadVersion;

    protected virtual Task RequestRenderAsync() => InvokeAsync(StateHasChanged);

    private async Task RenderIfCurrentAsync(long loadVersion)
    {
        if(!IsCurrentLoad(loadVersion))
            return;

        try
        {
            await RequestRenderAsync();
        }
        catch(InvalidOperationException) when(_disposed)
        {
        }
    }

    private void NavigateToTournament(Guid tournamentId)
    {
        NavigationManager.NavigateTo($"/tournaments/{tournamentId}");
    }

    private void StartEditingFeatured() => _isEditingFeatured = true;

    private void CancelEditingFeatured() => _isEditingFeatured = false;

    // Adopt the confirmed server order so the featured cards and the hero both reflect the save.
    private void ApplyFeaturedOrder(IReadOnlyList<Tournament> order)
    {
        // Retire any featured read that started before the save so its response cannot restore the old order.
        _featuredLoadVersion++;
        _featuredTournaments = order.ToList();
        PersistedFeaturedTournaments = _featuredTournaments;
        PersistedFeaturedLoadFailed = false;
        _isTournamentsLoading = false;
        _tournamentsError = null;
        _isEditingFeatured = false;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _loadVersion++;
        return ValueTask.CompletedTask;
    }
}
