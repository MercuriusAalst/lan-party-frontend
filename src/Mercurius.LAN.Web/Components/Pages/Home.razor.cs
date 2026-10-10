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

    private List<Tournament> _featuredTournaments = [];
    private List<Sponsor> _sponsors = [];
    private bool _isTournamentsLoading = true;
    private bool _isSponsorsLoading = true;
    private string? _tournamentsError;
    private string? _sponsorsError;
    private long _loadVersion;
    private bool _disposed;

    private IReadOnlyList<Tournament> FeaturedTournaments => _featuredTournaments;
    private IReadOnlyList<Tournament> HeroTournaments => _featuredTournaments.Take(3).ToList();

    private string EventWindow => EventOptions.Value.EventWindow;

    private string HeroLocation => $"{EventOptions.Value.VenueName}, {EventOptions.Value.Address}";

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if(firstRender && !_disposed)
            _ = LoadHomeDataAsync();

        return Task.CompletedTask;
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

        await RenderIfCurrentAsync(loadVersion);
        _ = LoadTournamentsAsync(loadVersion);
        _ = LoadSponsorsAsync(loadVersion);
    }

    private async Task LoadTournamentsAsync(long loadVersion)
    {
        try
        {
            var featured = await TournamentService.GetFeaturedTournamentsAsync();
            if(IsCurrentLoad(loadVersion))
                _featuredTournaments = featured?.Tournaments ?? [];
        }
        catch(Exception)
        {
            if(!IsCurrentLoad(loadVersion))
                return;

            _featuredTournaments = [];
            _tournamentsError = Localization["General.Home.LoadError"];
            ToastService.ShowError(Localization["General.Home.LoadToast"]);
        }
        finally
        {
            if(IsCurrentLoad(loadVersion))
            {
                _isTournamentsLoading = false;
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
                _sponsors = sponsors;
        }
        catch(Exception)
        {
            if(!IsCurrentLoad(loadVersion))
                return;

            _sponsors = [];
            _sponsorsError = Localization["General.Home.SponsorsUnavailable"];
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

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _loadVersion++;
        return ValueTask.CompletedTask;
    }
}
