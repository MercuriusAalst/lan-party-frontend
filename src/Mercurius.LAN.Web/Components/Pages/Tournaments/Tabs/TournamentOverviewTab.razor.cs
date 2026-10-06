using System.Net;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Refit;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;

public partial class TournamentOverviewTab
{
    [Parameter] public TournamentExtended Tournament { get; set; } = null!;
    [Parameter] public EventCallback<TournamentExtended> OnTournamentUpdated { get; set; }

    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    private bool _isEditMode;
    private UpdateTournamentDTO _editTournament = new();
    private EditContext? _editContext;
    private CustomInputFile? _imageInputRef;
    private string? _saveError;
    private bool _isSaving;

    private static readonly BracketType[] SupportedBracketTypes =
    [
        BracketType.SingleElimination,
        BracketType.DoubleElimination,
        BracketType.Leaderboard
    ];

    private static readonly IReadOnlyDictionary<string, string> ValidationFieldLabelKeys = new Dictionary<string, string>
    {
        [nameof(UpdateTournamentDTO.Name)] = "shared.name",
        [nameof(UpdateTournamentDTO.BracketType)] = "Feature.tournament.bracketType",
        [nameof(UpdateTournamentDTO.LeaderboardRankingMetric)] = "Feature.tournaments.rankingMetric",
        [nameof(UpdateTournamentDTO.Format)] = "Feature.tournaments.matchFormat",
        [nameof(UpdateTournamentDTO.FinalsFormat)] = "Feature.tournaments.finalsFormat",
        [nameof(UpdateTournamentDTO.ParticipationMode)] = "tournament.participation",
        [nameof(UpdateTournamentDTO.Image)] = "Feature.tournaments.tournamentImage",
        [nameof(UpdateTournamentDTO.TeamSize)] = "tournament.teamSize",
        [nameof(UpdateTournamentDTO.PlannedStartTime)] = "Feature.tournaments.plannedStartTime",
        [nameof(UpdateTournamentDTO.AverageGameDurationMinutes)] = "Feature.tournaments.averageGameDuration",
        [nameof(UpdateTournamentDTO.RoundBreakDurationMinutes)] = "Feature.tournaments.roundBreakDuration"
    };

    private static readonly IReadOnlyDictionary<string, string> ValidationMessageKeys = new Dictionary<string, string>
    {
        ["Planned start time is required."] = "form.plannedStartTimeRequired",
        ["Team tournaments require a team size between 1 and 50."] = "form.teamSizeRange",
        ["Leaderboard tournaments require a ranking metric."] = "form.leaderboardMetricRequired",
        ["Leaderboard tournaments are individual competitions."] = "form.leaderboardIndividualOnly"
    };

    private bool IsLeaderboard => Tournament.BracketType == BracketType.Leaderboard;

    private bool IsLeaderboardEdit => _editTournament.BracketType == BracketType.Leaderboard;

    private void EnableEditMode()
    {
        _isEditMode = true;
        _editTournament = new UpdateTournamentDTO
        {
            Name = Tournament.Name,
            Format = Tournament.Format,
            FinalsFormat = Tournament.FinalsFormat,
            BracketType = Tournament.BracketType,
            LeaderboardRankingMetric = Tournament.LeaderboardRankingMetric,
            ParticipationMode = Tournament.ParticipationMode,
            TeamSize = Tournament.TeamSize,
            PlannedStartTime = Tournament.PlannedStartTime.ToLocalDisplayTime(),
            AverageGameDurationMinutes = Tournament.AverageGameDurationMinutes > 0 ? Tournament.AverageGameDurationMinutes : 30,
            RoundBreakDurationMinutes = Tournament.RoundBreakDurationMinutes > 0 ? Tournament.RoundBreakDurationMinutes : 10
        };
        _editContext = new(_editTournament);
        _editContext.SetFieldCssClassProvider(new BootstrapValidationFieldClassProvider());
        _editContext.OnFieldChanged += (sender, args) =>
        {
            if(args.FieldIdentifier.FieldName == nameof(UpdateTournamentDTO.BracketType))
                ApplyBracketTypeDefaults();
            _editContext.Validate();
        };
    }

    internal void ApplyBracketTypeDefaults()
    {
        if(!IsLeaderboardEdit)
        {
            _editTournament.LeaderboardRankingMetric = null;
            return;
        }

        _editTournament.ParticipationMode = ParticipationMode.Individual;
        _editTournament.TeamSize = null;
    }

    internal string ResolveEditSaveError(ApiException exception)
    {
        // The backend signals a concurrent leaderboard change with the "leaderboard_changed" code.
        return string.Equals(exception.GetApiError()?.Code, "leaderboard_changed", StringComparison.OrdinalIgnoreCase)
            ? Localization["Feature.tournaments.editConflict"]
            : Localization["Feature.tournaments.updateFailed"];
    }

    private void CancelEditMode()
    {
        _isEditMode = false;
    }

    private string GetPlannedStartLabel() =>
        FormatDateTime(Tournament.PlannedStartTime);

    private string GetEstimatedEndLabel() =>
        Tournament.EstimatedEndTime.HasValue ? FormatDateTime(Tournament.EstimatedEndTime.Value) : Localization["Feature.tournamentsOverview.estimateUnavailable"];

    private string FormatDateTime(DateTime dateTime) =>
        Localization.FormatDateTime(dateTime.ToLocalDisplayTime());

    private string GetRegistrationStateLabel()
    {
        if(Tournament.Status != TournamentStatus.Scheduled)
            return Localization["Feature.tournaments.registrationClosed"];

        var activeRegistrationCount = Tournament.Registrations?.Count(registration =>
            registration.Status == TournamentRegistrationStatus.Active) ?? 0;
        var participantLabel = Tournament.ParticipationMode == ParticipationMode.Team
            ? Localization["Feature.tournament.participationTeamPlural"]
            : Localization["Feature.tournament.participationIndividualPlural"];
        return activeRegistrationCount == 0
            ? Localization["Feature.tournaments.registrationOpenEmpty"]
            : Localization.Get("Feature.tournaments.registrationOpenCount", activeRegistrationCount, participantLabel);
    }

    private async Task SubmitEditAsync()
    {
        if(_isSaving)
            return;

        _saveError = null;
        _isSaving = true;
        string? tempFilePath = _imageInputRef?.TempFilePath;
        string? contentType = _imageInputRef?.FileContentType;
        string? fileName = _imageInputRef?.FileName;

        try
        {
            var updatedTournament = await TournamentService.UpdateTournamentAsync(Tournament.Id, _editTournament, tempFilePath, contentType, fileName);
            Tournament.Name = updatedTournament.Name;
            Tournament.Format = updatedTournament.Format;
            Tournament.FinalsFormat = updatedTournament.FinalsFormat;
            Tournament.BracketType = updatedTournament.BracketType;
            Tournament.LeaderboardRankingMetric = updatedTournament.LeaderboardRankingMetric;
            Tournament.ParticipationMode = updatedTournament.ParticipationMode;
            Tournament.TeamSize = updatedTournament.TeamSize;
            Tournament.PlannedStartTime = updatedTournament.PlannedStartTime;
            Tournament.AverageGameDurationMinutes = updatedTournament.AverageGameDurationMinutes;
            Tournament.RoundBreakDurationMinutes = updatedTournament.RoundBreakDurationMinutes;
            Tournament.EstimatedEndTime = updatedTournament.EstimatedEndTime;
            Tournament.ImageUrl = updatedTournament.ImageUrl;
            _isEditMode = false;
            ToastService.ShowSuccess(Localization["Feature.tournaments.editSuccess"]);
            await OnTournamentUpdated.InvokeAsync(Tournament);
            await InvokeAsync(StateHasChanged);
        }
        catch(ApiException ex)
        {
            _saveError = ResolveEditSaveError(ex);
            ToastService.ShowError(_saveError);
        }
        catch(UnauthorizedAccessException)
        {
            _saveError = Localization["Feature.tournaments.updateUnauthorized"];
            ToastService.ShowError(_saveError);
        }
        catch(Exception)
        {
            _saveError = Localization["Feature.tournaments.updateFailedNow"];
            ToastService.ShowError(_saveError);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private string GetStatusLabel(TournamentStatus status) => status switch
    {
        TournamentStatus.Scheduled => Localization["Feature.tournament.statusScheduled"],
        TournamentStatus.InProgress => Localization["Feature.tournament.statusInProgress"],
        TournamentStatus.Completed => Localization["Feature.tournament.statusCompleted"],
        TournamentStatus.Canceled => Localization["Feature.tournament.statusCanceled"],
        _ => status.ToString()
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

    private string GetRankingMetricLabel(LeaderboardRankingMetric metric) => metric switch
    {
        LeaderboardRankingMetric.HighestScore => Localization["Feature.tournaments.rankingMetricHighestScore"],
        LeaderboardRankingMetric.FastestTime => Localization["Feature.tournaments.rankingMetricFastestTime"],
        _ => metric.ToString()
    };

    private string GetParticipationLabel(ParticipationMode mode) => mode switch
    {
        ParticipationMode.Individual => Localization["Feature.tournament.participationIndividual"],
        ParticipationMode.Team => Localization["Feature.tournament.participationTeam"],
        _ => mode.ToString()
    };

    private string GetFormatLabel(TournamentFormat format) => format switch
    {
        TournamentFormat.BestOf1 => Localization["Feature.tournament.formatBestOf1"],
        TournamentFormat.BestOf3 => Localization["Feature.tournament.formatBestOf3"],
        TournamentFormat.BestOf5 => Localization["Feature.tournament.formatBestOf5"],
        _ => format.ToString()
    };
}
