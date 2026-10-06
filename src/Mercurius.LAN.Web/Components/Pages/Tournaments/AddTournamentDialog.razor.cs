using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Refit;
using System.Net;
using System.Text.Json;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments;

public partial class AddTournamentDialog
{
    [Parameter]
    public EventCallback<TournamentExtended?> OnClose { get; set; }

    [Inject]
    private ITournamentService TournamentService { get; set; } = null!;
    [Inject]
    private IToastService ToastService { get; set; } = null!;
    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    private CreateTournamentDTO _newTournament = new();
    private bool _isDialogOpen = true;
    private EditContext? _editContext;
    private CustomInputFile? _imageInputRef;
    private DateTime? _plannedStartDate;
    private TimeSpan? _plannedStartTime;
    private string? _plannedStartTimeError;
    private string? _submitError;
    private bool _isSubmitting;

    private static readonly BracketType[] SupportedBracketTypes =
    [
        BracketType.SingleElimination,
        BracketType.DoubleElimination,
        BracketType.Leaderboard
    ];

    private static readonly IReadOnlyDictionary<string, string> ValidationFieldLabelKeys = new Dictionary<string, string>
    {
        [nameof(CreateTournamentDTO.Name)] = "shared.name",
        [nameof(CreateTournamentDTO.BracketType)] = "Feature.tournament.bracketType",
        [nameof(CreateTournamentDTO.LeaderboardRankingMetric)] = "Feature.tournaments.rankingMetric",
        [nameof(CreateTournamentDTO.Format)] = "tournament.format",
        [nameof(CreateTournamentDTO.FinalsFormat)] = "Feature.tournaments.finalsFormat",
        [nameof(CreateTournamentDTO.ParticipationMode)] = "tournament.participation",
        [nameof(CreateTournamentDTO.Image)] = "Feature.tournaments.tournamentImage",
        [nameof(CreateTournamentDTO.TeamSize)] = "tournament.teamSize",
        [nameof(CreateTournamentDTO.PlannedStartTime)] = "Feature.tournaments.plannedStartTime",
        [nameof(CreateTournamentDTO.AverageGameDurationMinutes)] = "Feature.tournaments.averageGameDuration",
        [nameof(CreateTournamentDTO.RoundBreakDurationMinutes)] = "Feature.tournaments.roundBreakDuration"
    };

    private static readonly IReadOnlyDictionary<string, string> ValidationMessageKeys = new Dictionary<string, string>
    {
        ["Planned start time is required."] = "form.plannedStartTimeRequired",
        ["Team tournaments require a team size between 1 and 50."] = "form.teamSizeRange",
        ["Leaderboard tournaments require a ranking metric."] = "form.leaderboardMetricRequired",
        ["Leaderboard tournaments are individual competitions."] = "form.leaderboardIndividualOnly"
    };

    private bool IsLeaderboardSelected => _newTournament.BracketType == BracketType.Leaderboard;


    protected override void OnInitialized() {

        SetPlannedStartInputs(_newTournament.PlannedStartTime);
        _editContext = new(_newTournament);
        _editContext.SetFieldCssClassProvider(new BootstrapValidationFieldClassProvider());
        _editContext.OnFieldChanged += (sender, args) => {
            if(args.FieldIdentifier.FieldName == nameof(CreateTournamentDTO.BracketType))
                ApplyBracketTypeDefaults();
            _editContext.Validate();
        };
    }

    internal void ApplyBracketTypeDefaults()
    {
        if(!IsLeaderboardSelected)
        {
            _newTournament.LeaderboardRankingMetric = null;
            return;
        }

        _newTournament.ParticipationMode = ParticipationMode.Individual;
        _newTournament.TeamSize = null;
    }
    private async Task SubmitTournamentAsync(EditContext editContext)
    {
        if(_isSubmitting)
            return;

        _submitError = null;
        string? tempFilePath = _imageInputRef?.TempFilePath;
        string? contentType = _imageInputRef?.FileContentType;
        string? fileName = _imageInputRef?.FileName;

        if(!TryApplyPlannedStartTime())
            return;

        _isSubmitting = true;
        try
        {
            var createdTournament = await TournamentService.CreateTournamentAsync(_newTournament, tempFilePath, contentType, fileName);
            ToastService.ShowSuccess(Localization.Get("Feature.tournaments.created", createdTournament.Name));
            await OnClose.InvokeAsync(createdTournament);
        }
        catch(ApiException ex)
        {
            _submitError = ResolveCreateError(ex);
            ToastService.ShowError(_submitError);
        }
        catch(UnauthorizedAccessException)
        {
            _submitError = Localization["Feature.tournaments.createUnauthorized"];
            ToastService.ShowError(_submitError);
        }
        catch(Exception)
        {
            _submitError = Localization["Feature.tournaments.createFailedNow"];
            ToastService.ShowError(_submitError);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    internal string ResolveCreateError(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if(exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return Localization["Feature.tournaments.createUnauthorized"];

        if(IsClientError(exception.StatusCode) && TryReadStructuredMessage(exception.Content, out var validationMessage))
            return validationMessage;

        return Localization["Feature.tournaments.createFailed"];
    }

    private static bool IsClientError(HttpStatusCode statusCode) =>
        (int)statusCode is >= 400 and < 500;

    // Only intentional copy from a structured JSON error object is surfaced. Plain-text bodies, JSON
    // string roots, and objects without a recognized message, detail, or validation-error field fall
    // back to the localized message, so a raw response body can never reach the dialog. The
    // ProblemDetails "title" field is deliberately ignored because RFC 7807 leaves it as a generic
    // HTTP reason phrase rather than copy meant for a person.
    private static bool TryReadStructuredMessage(string? content, out string message)
    {
        message = string.Empty;
        if(string.IsNullOrWhiteSpace(content))
            return false;

        try
        {
            using var document = JsonDocument.Parse(content);
            if(document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var candidate = ReadStringProperty(document.RootElement, "message")
                ?? ReadStringProperty(document.RootElement, "detail")
                ?? ReadFirstValidationError(document.RootElement);

            if(string.IsNullOrWhiteSpace(candidate))
                return false;

            candidate = candidate.Trim();
            if(candidate.Length == 0 || LooksLikeTransportDetail(candidate))
                return false;

            message = candidate;
            return true;
        }
        catch(JsonException)
        {
            return false;
        }
    }

    private static string? ReadStringProperty(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadFirstValidationError(JsonElement root)
    {
        if(!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return null;

        foreach(var error in errors.EnumerateObject())
        {
            if(error.Value.ValueKind == JsonValueKind.Array)
            {
                foreach(var entry in error.Value.EnumerateArray())
                {
                    if(entry.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.GetString()))
                        return entry.GetString();
                }
            }
            else if(error.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(error.Value.GetString()))
            {
                return error.Value.GetString();
            }
        }

        return null;
    }

    private static bool LooksLikeTransportDetail(string message)
    {
        var trimmed = message.TrimStart();
        return trimmed.StartsWith('{')
            || trimmed.StartsWith('[')
            || trimmed.StartsWith('<')
            || trimmed.Contains("traceId", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryApplyPlannedStartTime()
    {
        _plannedStartTimeError = null;

        if(!_plannedStartDate.HasValue)
        {
            _plannedStartTimeError = Localization["Feature.tournaments.invalidStartDate"];
            return false;
        }

        if(!_plannedStartTime.HasValue)
        {
            _plannedStartTimeError = Localization["Feature.tournaments.invalidStartTime"];
            return false;
        }

        var plannedStartTime = _plannedStartDate.Value
            .Date
            .Add(_plannedStartTime.Value);

        _newTournament.PlannedStartTime = DateTime.SpecifyKind(plannedStartTime, DateTimeKind.Local);
        return true;
    }

    private void HandlePlannedStartDateChanged(DateTime? plannedStartDate)
    {
        _plannedStartDate = plannedStartDate;
    }

    private void HandlePlannedStartTimeChanged(TimeSpan? plannedStartTime)
    {
        _plannedStartTime = plannedStartTime;
    }

    private void SetPlannedStartInputs(DateTime plannedStartTime)
    {
        _plannedStartDate = plannedStartTime.Date;
        _plannedStartTime = plannedStartTime.TimeOfDay;
    }

    private void CloseDialog(TournamentExtended? createdTournament)
    {
        _isDialogOpen = false;
        OnClose.InvokeAsync(createdTournament);
    }

    private string GetBracketLabel(BracketType type) => type switch
    {
        BracketType.SingleElimination => Localization["Feature.tournament.bracketSingle"],
        BracketType.DoubleElimination => Localization["Feature.tournament.bracketDouble"],
        BracketType.RoundRobin => Localization["Feature.tournament.bracketRoundRobin"],
        BracketType.Swiss => Localization["Feature.tournament.bracketSwiss"],
        BracketType.Leaderboard => Localization["Feature.tournament.bracketLeaderboard"],
        _ => type.ToString()
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

    private string GetRankingMetricLabel(LeaderboardRankingMetric metric) => metric switch
    {
        LeaderboardRankingMetric.HighestScore => Localization["Feature.tournaments.rankingMetricHighestScore"],
        LeaderboardRankingMetric.FastestTime => Localization["Feature.tournaments.rankingMetricFastestTime"],
        _ => metric.ToString()
    };
}
