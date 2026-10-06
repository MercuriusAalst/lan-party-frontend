using Blazored.Toast.Services;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments;

public partial class TournamentsOverview : IAsyncDisposable
{
    private enum TournamentSortOption
    {
        StartTime,
        Name,
        Status
    }

    private enum OverviewStatusFilter
    {
        All,
        Open,
        Ongoing,
        Finished,
        Cancelled
    }

    private enum OverviewParticipationFilter
    {
        All,
        Solo,
        Team
    }

    private IReadOnlyList<(OverviewStatusFilter Value, string Label)> StatusFilters =>
    [
        (OverviewStatusFilter.All, Localization["Feature.tournamentsOverview.filterAll"]),
        (OverviewStatusFilter.Open, Localization["Feature.tournamentsOverview.filterOpen"]),
        (OverviewStatusFilter.Ongoing, Localization["Feature.tournamentsOverview.filterOngoing"]),
        (OverviewStatusFilter.Finished, Localization["Feature.tournamentsOverview.filterFinished"]),
        (OverviewStatusFilter.Cancelled, Localization["Feature.tournamentsOverview.filterCancelled"])
    ];

    private IReadOnlyList<(OverviewParticipationFilter Value, string Label)> ParticipationFilters =>
    [
        (OverviewParticipationFilter.All, Localization["Feature.tournamentsOverview.filterAll"]),
        (OverviewParticipationFilter.Solo, Localization["Feature.tournamentsOverview.filterSolo"]),
        (OverviewParticipationFilter.Team, Localization["Feature.tournamentsOverview.filterTeam"])
    ];

    private List<Tournament> _tournaments = [];
    private List<Sponsor> _sponsors = [];
    private string _searchTerm = string.Empty;
    private bool _isAddTournamentDialogOpen;
    private bool _isLoading = true;
    private bool _isLoadingPage;
    private bool _isSponsorsLoading;
    private bool _isRegistrationDialogOpen;
    private TournamentExtended? _registrationTournament;
    private string? _loadError;
    private string? _sponsorsError;
    private int _page = 1;
    private const int PageSize = 24;
    private const int PageRequestSize = PageSize + 1;
    private bool _hasNextPage;
    private long _tournamentLoadVersion;
    private long _sponsorLoadVersion;
    private bool _disposed;
    private TournamentSortOption _sortOption;
    private OverviewStatusFilter _statusFilter = OverviewStatusFilter.All;
    private OverviewParticipationFilter _participationFilter = OverviewParticipationFilter.All;

    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private ISponsorService SponsorService { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    private List<Tournament> FilteredTournaments => ApplySort(ApplyFilters()).ToList();
    private int OpenRegistrationCount => FilteredTournaments.Count(CanRegister);

    private string ResultsHeading => Localization.Get("Feature.tournamentsOverview.resultsHeading", FilteredTournaments.Count);

    private string ResultsSummary
    {
        get
        {
            if(FilteredTournaments.Count == 0)
                return Localization["Feature.tournamentsOverview.noSelectedMatches"];

            return Localization.Get("Feature.tournamentsOverview.resultsSummary", OpenRegistrationCount, _tournaments.Select(Tournament => Tournament.Format).Distinct().Count());
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if(firstRender && !_disposed)
            _ = LoadPageAsync(showLoading: true);

        return Task.CompletedTask;
    }

    private async Task LoadPageAsync(bool showLoading)
    {
        if(_disposed)
            return;

        if(showLoading)
            _isLoading = true;
        else
            _isLoadingPage = true;

        _loadError = null;
        var tournamentLoadVersion = ++_tournamentLoadVersion;
        var shouldLoadSponsors = _sponsors.Count == 0 && !_isSponsorsLoading;
        var sponsorLoadVersion = _sponsorLoadVersion;
        if(shouldLoadSponsors)
        {
            _isSponsorsLoading = true;
            _sponsorsError = null;
            sponsorLoadVersion = ++_sponsorLoadVersion;
        }

        await RenderIfActiveAsync();
        _ = LoadTournamentsAsync(tournamentLoadVersion);
        if(shouldLoadSponsors)
            _ = LoadSponsorsAsync(sponsorLoadVersion);
    }

    private async Task LoadTournamentsAsync(long loadVersion)
    {
        try
        {
            var fetchedTournaments = await TournamentService.GetTournamentsAsync(_page, PageRequestSize);
            if(!IsCurrentTournamentLoad(loadVersion))
                return;

            _hasNextPage = fetchedTournaments.Count > PageSize;
            _tournaments = fetchedTournaments.Take(PageSize).ToList();
        }
        catch(Exception exception)
        {
            if(!IsCurrentTournamentLoad(loadVersion))
                return;

            _tournaments = [];
            _hasNextPage = false;
            _loadError = exception is UnauthorizedAccessException
                ? Localization["Feature.tournamentsOverview.signInToLoad"]
                : Localization["Feature.tournamentsOverview.loadError"];
            ToastService.ShowError(_loadError);
        }
        finally
        {
            if(IsCurrentTournamentLoad(loadVersion))
            {
                _isLoading = false;
                _isLoadingPage = false;
                await RenderIfActiveAsync();
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
            if(IsCurrentSponsorLoad(loadVersion))
                _sponsors = sponsors;
        }
        catch(Exception)
        {
            if(IsCurrentSponsorLoad(loadVersion))
                _sponsorsError = Localization["General.Home.SponsorsUnavailable"];
        }
        finally
        {
            if(IsCurrentSponsorLoad(loadVersion))
            {
                _isSponsorsLoading = false;
                await RenderIfActiveAsync();
            }
        }
    }

    private bool IsCurrentTournamentLoad(long loadVersion) =>
        !_disposed && loadVersion == _tournamentLoadVersion;

    private bool IsCurrentSponsorLoad(long loadVersion) =>
        !_disposed && loadVersion == _sponsorLoadVersion;

    protected virtual Task RequestRenderAsync() => InvokeAsync(StateHasChanged);

    private async Task RenderIfActiveAsync()
    {
        if(_disposed)
            return;

        try
        {
            await RequestRenderAsync();
        }
        catch(InvalidOperationException) when(_disposed)
        {
        }
    }

    private async Task ChangePageAsync(int page)
    {
        if(page < 1 || page == _page || (page > _page && !_hasNextPage))
            return;

        _page = page;
        await LoadPageAsync(showLoading: false);
    }

    private Task RetryLoadAsync()
    {
        _page = 1;
        return LoadPageAsync(showLoading: true);
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _tournamentLoadVersion++;
        _sponsorLoadVersion++;
        return ValueTask.CompletedTask;
    }

    private void NavigateToTournamentDetail(Guid tournamentId)
    {
        NavigationManager.NavigateTo($"/tournaments/{tournamentId}");
    }

    private void ShowAddTournamentDialog()
    {
        _isAddTournamentDialogOpen = true;
    }

    private async Task CloseAddTournamentDialog(TournamentExtended? createdTournament)
    {
        _isAddTournamentDialogOpen = false;
        if(createdTournament != null)
        {
            _tournaments.Add(createdTournament);
            await InvokeAsync(StateHasChanged);
        }
    }

    private void OpenRegistration(Tournament tournament)
    {
        if(tournament.BracketType == BracketType.Leaderboard)
        {
            ToastService.ShowWarning(Localization["Feature.tournaments.leaderboardNoRegistration"]);
            return;
        }

        if(tournament.Status != TournamentStatus.Scheduled)
        {
            ToastService.ShowWarning(Localization["tournament.registrationClosedShort"]);
            return;
        }

        _registrationTournament = new TournamentExtended
        {
            Id = tournament.Id,
            Name = tournament.Name,
            StartTime = tournament.StartTime,
            EndTime = tournament.EndTime,
            PlannedStartTime = tournament.PlannedStartTime,
            AverageGameDurationMinutes = tournament.AverageGameDurationMinutes,
            RoundBreakDurationMinutes = tournament.RoundBreakDurationMinutes,
            EstimatedEndTime = tournament.EstimatedEndTime,
            ImageUrl = tournament.ImageUrl,
            Status = tournament.Status,
            BracketType = tournament.BracketType,
            Format = tournament.Format,
            FinalsFormat = tournament.FinalsFormat,
            ParticipationMode = tournament.ParticipationMode,
            TeamSize = tournament.TeamSize
        };
        _isRegistrationDialogOpen = true;
    }

    private void HandleRegistrationDialogOpenChanged(bool isOpen)
    {
        _isRegistrationDialogOpen = isOpen;
        if(!isOpen)
            _registrationTournament = null;
    }

    private void SetStatusFilter(OverviewStatusFilter filter)
    {
        _statusFilter = filter;
    }

    private void SetParticipationFilter(OverviewParticipationFilter filter)
    {
        _participationFilter = filter;
    }

    private IEnumerable<Tournament> ApplyFilters()
    {
        return _tournaments
            .Where(MatchesSearch)
            .Where(MatchesStatusFilter)
            .Where(MatchesParticipationFilter);
    }

    private IEnumerable<Tournament> ApplySort(IEnumerable<Tournament> tournaments)
    {
        return _sortOption switch
        {
            TournamentSortOption.Name => tournaments.OrderBy(tournament => tournament.Name),
            TournamentSortOption.Status => tournaments.OrderBy(GetStatusOrder).ThenBy(GetPlannedStartForSort),
            _ => tournaments.OrderBy(GetPlannedStartForSort)
        };
    }

    private bool MatchesSearch(Tournament tournament)
    {
        return string.IsNullOrWhiteSpace(_searchTerm) ||
            tournament.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesStatusFilter(Tournament tournament)
    {
        return _statusFilter switch
        {
            OverviewStatusFilter.Open => tournament.Status == TournamentStatus.Scheduled,
            OverviewStatusFilter.Ongoing => tournament.Status == TournamentStatus.InProgress,
            OverviewStatusFilter.Finished => tournament.Status == TournamentStatus.Completed,
            OverviewStatusFilter.Cancelled => tournament.Status == TournamentStatus.Canceled,
            _ => true
        };
    }

    private bool MatchesParticipationFilter(Tournament tournament)
    {
        return _participationFilter switch
        {
            OverviewParticipationFilter.Solo => tournament.ParticipationMode == ParticipationMode.Individual,
            OverviewParticipationFilter.Team => tournament.ParticipationMode == ParticipationMode.Team,
            _ => true
        };
    }

    private static int GetStatusOrder(Tournament tournament)
    {
        return tournament.Status switch
        {
            TournamentStatus.Scheduled => 0,
            TournamentStatus.InProgress => 1,
            TournamentStatus.Completed => 2,
            TournamentStatus.Canceled => 3,
            _ => 4
        };
    }

    private string FormatDateTime(DateTime dateTime)
    {
        return Localization.FormatDateTime(dateTime.ToLocalDisplayTime());
    }

    private static DateTime GetPlannedStartForSort(Tournament tournament)
    {
        return tournament.PlannedStartTime;
    }

    private string GetPlannedStartLabel(Tournament tournament)
    {
        return FormatDateTime(tournament.PlannedStartTime);
    }

    private string GetEstimatedEndLabel(Tournament tournament)
    {
        return tournament.EstimatedEndTime.HasValue
            ? FormatDateTime(tournament.EstimatedEndTime.Value)
            : Localization["Feature.tournamentsOverview.estimateUnavailable"];
    }

    private static bool CanRegister(Tournament tournament) =>
        tournament.BracketType != BracketType.Leaderboard &&
        tournament.Status == TournamentStatus.Scheduled;

    private string GetStatusLabel(TournamentStatus status) => status switch
    {
        TournamentStatus.Scheduled => Localization["Feature.tournament.statusScheduled"],
        TournamentStatus.InProgress => Localization["Feature.tournament.statusInProgress"],
        TournamentStatus.Completed => Localization["Feature.tournament.statusCompleted"],
        TournamentStatus.Canceled => Localization["Feature.tournament.statusCanceled"],
        _ => status.ToString()
    };

    private string GetParticipationLabel(ParticipationMode mode) => mode switch
    {
        ParticipationMode.Individual => Localization["Feature.tournament.participationIndividual"],
        ParticipationMode.Team => Localization["Feature.tournament.participationTeam"],
        _ => mode.ToString()
    };

    private string GetBracketLabel(BracketType bracketType) => bracketType switch
    {
        BracketType.SingleElimination => Localization["Feature.tournament.bracketSingle"],
        BracketType.DoubleElimination => Localization["Feature.tournament.bracketDouble"],
        BracketType.RoundRobin => Localization["Feature.tournament.bracketRoundRobin"],
        BracketType.Swiss => Localization["Feature.tournament.bracketSwiss"],
        BracketType.Leaderboard => Localization["Feature.tournament.bracketLeaderboard"],
        _ => bracketType.ToString()
    };

    private string GetFormatLabel(TournamentFormat format) => format switch
    {
        TournamentFormat.BestOf1 => Localization["Feature.tournament.formatBestOf1"],
        TournamentFormat.BestOf3 => Localization["Feature.tournament.formatBestOf3"],
        TournamentFormat.BestOf5 => Localization["Feature.tournament.formatBestOf5"],
        _ => format.ToString()
    };
}
