using System.Net;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using Refit;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments.Tabs;

public partial class TournamentParticipantsTab : IDisposable, IAsyncDisposable
{
    // The backend roster-eligibility endpoint rejects requests with more than 50 user ids.
    private const int MaxRosterEligibilityUserIds = 50;

    private static readonly HashSet<string> ExistingRegistrationConflictCodes =
    [
        "team_already_registered",
        "captain_duplicate_participation",
        "duplicate_participation",
        "roster_candidate_ineligible"
    ];

    [Parameter] public TournamentExtended Tournament { get; set; } = null!;
    [Parameter] public EventCallback<TournamentExtended> OnTournamentUpdated { get; set; }
    [Parameter] public bool RegistrationDialogOpen { get; set; }
    [Parameter] public EventCallback<bool> RegistrationDialogOpenChanged { get; set; }
    [Parameter] public bool PopupOnly { get; set; }

    [Inject] private ITeamService TeamService { get; set; } = null!;
    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;
    [Inject] private ITeamRealtimeService TeamRealtimeService { get; set; } = null!;
    [Inject] private ITeamNotificationService NotificationService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private readonly List<ParticipantViewModel> _participants = [];
    private readonly List<AdminTournamentRegistrationDTO> _adminRegistrations = [];
    private readonly HashSet<Guid> _selectedRosterUserIds = [];
    private readonly Dictionary<Guid, EligibilityResponseDTO> _teamEligibilityById = [];
    private readonly Dictionary<Guid, RosterCandidateEligibilityDTO> _rosterCandidatesById = [];

    private bool _isRegistrationDialogOpen;
    private ElementReference _registrationDialogElement;
    private ElementReference _teamUnregistrationTriggerElement;
    private ElementReference _teamUnregistrationKeepButtonElement;
    private IJSObjectReference? _registrationFocusTrap;
    private CurrentUserTournamentRegistrationStateDTO? _registrationState;
    private EligibilityResponseDTO? _individualEligibility;
    private EligibilityResponseDTO? _selectedTeamEligibility;
    private RosterCandidateEligibilityResponseDTO? _rosterEligibility;
    private CurrentUserTeamSummaryDTO? _teamSummary;
    private Guid? _selectedTeamId;
    private Guid? _pendingAdminRemovalRegistrationId;
    private string _adminRemovalReason = string.Empty;
    private string? _registrationError;
    private string? _adminError;
    private string? _teamError;
    private bool _teamSummaryUnavailable;
    private bool _teamEligibilityUnavailable;
    private bool _rosterEligibilityUnavailable;
    private bool _isAuthenticated;
    private bool _isAdmin;
    private bool _isLoadingRegistration;
    private bool _isLoadingTeams;
    private bool _isLoadingRoster;
    private bool _isLoadingAdmin;
    private bool _isSubmitting;
    private bool _hasLoadedForTournament;
    private bool _isDisposed;
    private bool _hasDirtyRosterDraft;
    private bool _isTeamUnregistrationConfirmationOpen;
    private bool _isIndividualRegistrationConfirmationOpen;
    private bool _isIndividualUnregistrationConfirmationOpen;
    private bool _focusTeamUnregistrationConfirmation;
    private bool _restoreTeamUnregistrationFocus;
    private bool _registrationLoadRequested = true;
    private bool _registrationLoadCompleted;
    private bool _registrationLoadInFlight;
    private CancellationTokenSource? _registrationLoadCancellation;
    private readonly TeamStateInvalidationGate _teamStateInvalidationGate = new();
    private int _activeTeamStep;
    private long _requestGeneration;
    private long? _teamUnregistrationConfirmationGeneration;
    private Guid? _teamUnregistrationConfirmationTeamId;
    private Guid _loadedTournamentId;
    private string _loadedRegistrationFingerprint = string.Empty;
    private string? _registrationWarning;

    private IReadOnlyList<TeamManagementSummaryDTO> CaptainedTeams =>
        _teamSummary?.CaptainedTeams ?? [];

    private IReadOnlyList<TeamManagementSummaryDTO> RegistrableCaptainedTeams =>
        FilterCaptainedTeamsByRequiredSize(CaptainedTeams, RequiredTeamSize);

    private bool HasUndersizedCaptainedTeams =>
        RequiredTeamSize > 0 && CaptainedTeams.Any(team => team.Members.Count < RequiredTeamSize);

    private TeamManagementSummaryDTO? SelectedTeam =>
        _selectedTeamId.HasValue
            ? RegistrableCaptainedTeams.FirstOrDefault(team => team.Id == _selectedTeamId.Value)
            : null;

    private IReadOnlyList<PublicUserDTO> EditableRosterCandidates
    {
        get
        {
            var candidates = (SelectedTeam?.Members ?? []).ToList();
            var candidateIds = candidates.Select(member => member.Id).ToHashSet();
            foreach(var member in CaptainManagedRegistration?.RosterMembers ?? [])
            {
                if(member.User is not null && candidateIds.Add(member.User.Id))
                    candidates.Add(member.User);
            }

            foreach(var candidateId in MergeEditableRosterCandidateIds(
                        candidates.Select(member => member.Id),
                        _selectedRosterUserIds,
                        _rosterCandidatesById.Keys))
            {
                if(candidateIds.Add(candidateId))
                {
                    var candidateUser = _rosterCandidatesById.TryGetValue(candidateId, out var eligibilityCandidate)
                        ? eligibilityCandidate.User
                        : null;
                    candidates.Add(candidateUser ?? new PublicUserDTO
                    {
                        Id = candidateId,
                        DisplayName = Localization["Feature.tournaments.formerRosterMember"]
                    });
                }
            }

            if(SelectedTeam is { } team && team.CaptainUserId != Guid.Empty && candidateIds.Add(team.CaptainUserId))
            {
                candidates.Add(new PublicUserDTO
                {
                    Id = team.CaptainUserId,
                    Username = team.CaptainUsername,
                    DisplayName = string.IsNullOrWhiteSpace(team.CaptainUsername)
                        ? Localization["Feature.tournaments.currentCaptain"]
                        : team.CaptainUsername
                });
            }

            return candidates;
        }
    }

    private TournamentRegistrationDTO? CaptainManagedRegistration =>
        SelectedTeam is null
            ? null
            : (_registrationState?.CaptainManagedRegistrations ?? [])
                .FirstOrDefault(registration => registration.Team?.Id == SelectedTeam.Id);

    private TournamentRegistrationDTO? CurrentTeamRegistration =>
        _registrationState?.CurrentTeamRegistration ?? _registrationState?.ActiveTeamRegistration;

    private TournamentRegistrationDTO? CurrentCaptainManagedRegistration =>
        FindCaptainManagedRegistration(_registrationState?.CaptainManagedRegistrations ?? [], CaptainedTeams);

    private TournamentRegistrationDTO? TeamUnregistrationConfirmationRegistration =>
        _teamUnregistrationConfirmationTeamId is { } teamId
            ? (_registrationState?.CaptainManagedRegistrations ?? [])
                .FirstOrDefault(registration => registration.Team?.Id == teamId)
            : null;

    private bool HasCaptainManagedRegistration => CaptainManagedRegistration is not null;

    private int RequiredTeamSize => Tournament.TeamSize.GetValueOrDefault();

    private bool IsRegistrationOpen => Tournament.Status == TournamentStatus.Scheduled;

    private bool CanAdvanceFromTeamSelection =>
        SelectedTeam is not null &&
        _selectedTeamEligibility is not null &&
        CanUseTeamEligibility(SelectedTeam.Id, _selectedTeamEligibility);

    private bool HasLocalRosterShape
    {
        get
        {
            if(SelectedTeam is null || RequiredTeamSize <= 0)
                return false;

            var selection = GetNormalizedRosterSelection();
            return selection.Count == RequiredTeamSize &&
                   selection.Contains(SelectedTeam.CaptainUserId);
        }
    }

    private bool IsRosterEligibleForWorkflow
    {
        get
        {
            if(_rosterEligibility is null)
                return false;

            if(_rosterEligibility.Eligible)
                return true;

            if(!HasCaptainManagedRegistration ||
               _rosterEligibility.ReasonCodes.Count == 0 ||
               !_rosterEligibility.ReasonCodes.All(IsExistingRegistrationConflictCode))
                return false;

            var existingRosterUserIds = GetExistingRosterUserIds();
            return _rosterEligibility.Candidates.All(candidate =>
                candidate.Eligible ||
                IsExistingRosterConflictAllowed(candidate.UserId, candidate, existingRosterUserIds));
        }
    }

    private bool CanSubmitRoster =>
        IsRegistrationOpen &&
        !_isLoadingRegistration &&
        !_isLoadingRoster &&
        !_rosterEligibilityUnavailable &&
        SelectedTeam is not null &&
        CanAdvanceFromTeamSelection &&
        HasLocalRosterShape &&
        IsRosterEligibleForWorkflow;

    private bool CanAdvanceTeamStep => _activeTeamStep switch
    {
        0 => CanAdvanceFromTeamSelection,
        1 => HasLocalRosterShape && IsRosterEligibleForWorkflow,
        _ => false
    };

    private bool CanConfirmPending =>
        IsRegistrationOpen &&
        !_isLoadingRegistration &&
        _registrationState?.CanConfirmRoster == true &&
        _registrationState.PendingRosterConfirmation is not null;

    private bool CanUnregisterSelectedTeam =>
        SelectedTeam is not null && CanUnregisterTeam(SelectedTeam.Id);

    private bool CanCancelCaptainManagedRegistration =>
        CurrentCaptainManagedRegistration?.Team is { } team && CanUnregisterTeam(team.Id);

    private Guid? TeamCancellationTargetId =>
        GetTeamCancellationTarget(
            _activeTeamStep,
            SelectedTeam?.Id,
            CanUnregisterSelectedTeam,
            CurrentCaptainManagedRegistration?.Team?.Id,
            CanCancelCaptainManagedRegistration);

    private bool CanRegisterIndividualDirectly =>
        IsRegistrationOpen &&
        _isAuthenticated &&
        Tournament.ParticipationMode == ParticipationMode.Individual &&
        _registrationState is { IndividualRegistration: null, CanRegisterIndividual: true } &&
        _individualEligibility is { Eligible: true };

    private bool CanUnregisterIndividualDirectly =>
        IsRegistrationOpen &&
        _isAuthenticated &&
        Tournament.ParticipationMode == ParticipationMode.Individual &&
        _registrationState is { IndividualRegistration: not null, CanUnregister: true };

    private bool CanCancelTeamRegistrationDirectly =>
        !PopupOnly &&
        Tournament.ParticipationMode == ParticipationMode.Team &&
        CanCancelCaptainManagedRegistration;

    private bool CanUnregisterTeam(Guid teamId) =>
        IsRegistrationOpen &&
        !_isLoadingRegistration &&
        !_isLoadingRoster &&
        !_isSubmitting &&
        _registrationState?.CanUnregister == true &&
        (_registrationState?.CaptainManagedRegistrations ?? [])
            .Any(registration => registration.Team?.Id == teamId);

    private bool HasPublicParticipants => _participants.Count > 0;

    private bool HasDirtyRosterDraft => _hasDirtyRosterDraft;

    private string LoginUrl =>
        $"/account/login?returnUrl={Uri.EscapeDataString($"/tournaments/{Tournament.Id}#tournament-participants")}";

    protected override void OnInitialized()
    {
        TeamRealtimeService.TeamStateInvalidated += HandleTeamStateInvalidatedAsync;
        NotificationService.RosterDecisionChanged += HandleRosterDecisionChangedAsync;
    }

    protected override void OnParametersSet()
    {
        var wasRegistrationDialogOpen = _isRegistrationDialogOpen;
        if(_isRegistrationDialogOpen != RegistrationDialogOpen)
            _isRegistrationDialogOpen = RegistrationDialogOpen;

        if(_isRegistrationDialogOpen && !wasRegistrationDialogOpen && !_registrationLoadCompleted)
            _registrationLoadRequested = true;

        var registrationFingerprint = GetRegistrationFingerprint(Tournament);
        if(_loadedTournamentId != Tournament.Id || _loadedRegistrationFingerprint != registrationFingerprint)
        {
            var discardedDraft = _hasDirtyRosterDraft;
            CancelRegistrationLoad();
            _loadedTournamentId = Tournament.Id;
            _loadedRegistrationFingerprint = registrationFingerprint;
            _requestGeneration++;
            _hasLoadedForTournament = false;
            _registrationLoadCompleted = false;
            _registrationLoadRequested = true;
            ResetRegistrationContext();
            if(discardedDraft)
                _registrationWarning = Localization["tournament.registrationChanged"];
        }

        _participants.Clear();
        _participants.AddRange(BuildParticipants(Tournament));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(_restoreTeamUnregistrationFocus && !_isTeamUnregistrationConfirmationOpen)
        {
            _restoreTeamUnregistrationFocus = false;
            await _teamUnregistrationTriggerElement.FocusAsync();
        }

        if(_focusTeamUnregistrationConfirmation)
        {
            _focusTeamUnregistrationConfirmation = false;
            if(_isTeamUnregistrationConfirmationOpen)
                await _teamUnregistrationKeepButtonElement.FocusAsync();
        }

        if(_isRegistrationDialogOpen && _registrationFocusTrap is null)
        {
            _registrationFocusTrap = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "activateTeamModalFocusTrap",
                _registrationDialogElement);
        }
        else if(!_isRegistrationDialogOpen && _registrationFocusTrap is not null)
        {
            await DisposeRegistrationFocusTrapAsync();
        }

        if(!_registrationLoadRequested ||
           _hasLoadedForTournament ||
           Tournament.Id == Guid.Empty ||
           (PopupOnly && !_isRegistrationDialogOpen) ||
           _registrationLoadInFlight)
            return;

        _registrationLoadRequested = false;
        _hasLoadedForTournament = true;
        var generation = ++_requestGeneration;
        var cancellation = BeginRegistrationLoad();
        try
        {
            await LoadRegistrationContextAsync(
                preserveRosterDraft: _hasDirtyRosterDraft,
                requestGenerationOverride: generation,
                cancellationToken: cancellation.Token);
        }
        finally
        {
            EndRegistrationLoad(cancellation);
            if(IsCurrentRequest(Tournament.Id, generation))
                _hasLoadedForTournament = _registrationLoadCompleted;
        }
    }

    private static IEnumerable<ParticipantViewModel> BuildParticipants(TournamentExtended tournament)
    {
        var seen = new HashSet<Guid>();

        foreach(var registration in tournament.Registrations ?? [])
        {
            if(registration.Status != TournamentRegistrationStatus.Active)
                continue;

            if(registration.Kind == TournamentRegistrationKind.Individual && registration.User is not null)
            {
                if(seen.Add(registration.User.Id))
                    yield return ParticipantViewModel.FromUser(registration.User);

                continue;
            }

            if(registration.Kind == TournamentRegistrationKind.Team && registration.Team is not null && seen.Add(registration.Team.Id))
            {
                yield return ParticipantViewModel.FromTeam(new Team
                {
                    Id = registration.Team.Id,
                    Name = registration.Team.Name,
                    CaptainUserId = registration.Team.CaptainUserId,
                    LogoUrl = registration.Team.LogoUrl,
                    Members = (registration.RosterMembers ?? [])
                        .Where(member => member.User is not null)
                        .Select(member => member.User)
                        .ToList()
                });
            }
        }
    }

    private static string GetRegistrationFingerprint(TournamentExtended tournament) =>
        $"{tournament.Id:N}|{tournament.ParticipationMode}|{tournament.TeamSize?.ToString() ?? "none"}|{tournament.Status}|{tournament.PlannedStartTime.Ticks}|{tournament.StartTime.Ticks}|{tournament.EndTime.Ticks}";

    private async Task<bool> LoadRegistrationContextAsync(
        bool preserveRosterDraft = false,
        bool preserveSubmitting = false,
        long? requestGenerationOverride = null,
        CancellationToken cancellationToken = default)
    {
        var currentUserStateLoaded = false;
        var loadCompleted = false;
        var tournamentId = Tournament.Id;
        var generation = requestGenerationOverride ?? ++_requestGeneration;
        cancellationToken = cancellationToken == default
            ? _registrationLoadCancellation?.Token ?? CancellationToken.None
            : cancellationToken;
        var draftTeamId = preserveRosterDraft && _hasDirtyRosterDraft ? _selectedTeamId : null;
        var draftRosterUserIds = draftTeamId.HasValue ? _selectedRosterUserIds.ToHashSet() : null;
        var draftStep = draftTeamId.HasValue ? _activeTeamStep : 0;
        if(!draftTeamId.HasValue)
            _hasDirtyRosterDraft = false;

        _isLoadingRegistration = true;
        _isLoadingTeams = false;
        _isLoadingRoster = false;
        _isLoadingAdmin = false;
        _isSubmitting = preserveSubmitting;
        if(!preserveSubmitting)
            _pendingAdminRemovalRegistrationId = null;
        _registrationError = null;
        _adminError = null;
        _teamError = null;
        _teamSummaryUnavailable = false;
        _teamEligibilityUnavailable = false;
        _rosterEligibilityUnavailable = false;

        if(!_isDisposed)
            await InvokeAsync(StateHasChanged);

        try
        {
            var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                return currentUserStateLoaded;

            var principal = authenticationState.User;
            _isAuthenticated = principal.Identity?.IsAuthenticated == true;
            _isAdmin = _isAuthenticated && principal.IsInRole("admin");

            if(!_isAuthenticated)
            {
                _registrationState = null;
                _adminRegistrations.Clear();
                _individualEligibility = null;
                loadCompleted = true;
                return true;
            }

            try
            {
                var state = await TournamentService.GetCurrentUserTournamentRegistrationStateAsync(
                    tournamentId,
                    cancellationToken);
                currentUserStateLoaded = true;
                if(!cancellationToken.IsCancellationRequested && IsCurrentRequest(tournamentId, generation))
                    _registrationState = state;
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
            }
            catch(Exception exception) when(IsUnauthorized(exception))
            {
                if(IsCurrentRequest(tournamentId, generation))
                    _registrationError = Localization["Feature.tournaments.registrationStatusUnauthorized"];
            }
            catch(Exception exception)
            {
                if(IsCurrentRequest(tournamentId, generation))
                    _registrationError = GetLocalizedErrorMessage(exception, "Feature.tournaments.registrationStatusUnavailable");
            }

            if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                return currentUserStateLoaded;

            if(IsRegistrationOpen)
            {
                if(Tournament.ParticipationMode == ParticipationMode.Individual)
                    await LoadIndividualEligibilityAsync(tournamentId, generation, cancellationToken);
                else
                    await LoadTeamRegistrationContextAsync(
                        tournamentId,
                        generation,
                        draftTeamId,
                        draftRosterUserIds,
                        draftStep,
                        cancellationToken);
            }

            if(!PopupOnly && _isAdmin && IsCurrentRequest(tournamentId, generation))
                await LoadAdminRegistrationsAsync(tournamentId, generation, cancellationToken);

            loadCompleted = currentUserStateLoaded &&
                            _registrationError is null &&
                            (!IsRegistrationOpen || Tournament.ParticipationMode == ParticipationMode.Individual
                                ? _individualEligibility is not null || !IsRegistrationOpen
                                : !_teamSummaryUnavailable &&
                                  !_teamEligibilityUnavailable &&
                                  !_rosterEligibilityUnavailable) &&
                            (PopupOnly || !_isAdmin || _adminError is null);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
            // A newer load, dialog close, or component disposal owns the state now.
        }
        catch(OperationCanceledException)
        {
            // A newer tournament route or eligibility request owns the state now.
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, generation))
                _registrationError = GetLocalizedErrorMessage(exception, "Feature.tournaments.registrationOptionsUnavailable");
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, generation))
            {
                if(draftTeamId.HasValue && _selectedTeamId == draftTeamId)
                {
                    _hasDirtyRosterDraft = true;
                    _teamError ??= Localization["Feature.tournaments.liveUpdateDraftKept"];
                }
                else if(draftTeamId.HasValue)
                {
                    _hasDirtyRosterDraft = false;
                    if(SelectedTeam is null)
                        _selectedRosterUserIds.Clear();
                    _activeTeamStep = 0;
                    _registrationWarning =
                        Localization["Feature.tournaments.draftTeamUnavailable"];
                }
                _isLoadingRegistration = false;
                if(preserveSubmitting)
                {
                    _isSubmitting = false;
                    _pendingAdminRemovalRegistrationId = null;
                }
                _registrationLoadCompleted = loadCompleted && !cancellationToken.IsCancellationRequested;
                await InvokeAsync(StateHasChanged);
            }
        }

        return currentUserStateLoaded;
    }

    private async Task LoadIndividualEligibilityAsync(
        Guid tournamentId,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var eligibility = await TournamentService.CheckIndividualTournamentRegistrationEligibilityAsync(
                tournamentId,
                cancellationToken);
            if(!cancellationToken.IsCancellationRequested && IsCurrentRequest(tournamentId, generation))
                _individualEligibility = eligibility;
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
        }
        catch(Exception exception) when(IsUnauthorized(exception))
        {
            if(IsCurrentRequest(tournamentId, generation))
                _registrationError ??= Localization["Feature.tournaments.individualEligibilityUnauthorized"];
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, generation))
                _registrationError ??= GetLocalizedErrorMessage(exception, "Feature.tournaments.individualEligibilityUnavailable");
        }
    }

    private async Task LoadTeamRegistrationContextAsync(
        Guid tournamentId,
        long generation,
        Guid? draftTeamId,
        HashSet<Guid>? draftRosterUserIds,
        int draftStep,
        CancellationToken cancellationToken)
    {
        if(!IsCurrentRequest(tournamentId, generation))
            return;

        _isLoadingTeams = true;
        _teamSummaryUnavailable = false;
        _teamEligibilityUnavailable = false;
        _teamEligibilityById.Clear();
        _selectedTeamEligibility = null;
        _rosterEligibility = null;
        _rosterCandidatesById.Clear();
        await InvokeAsync(StateHasChanged);
        try
        {
            var teamSummary = await TeamService.GetCurrentUserTeamSummaryAsync(cancellationToken);
            if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                return;

            _teamSummary = teamSummary;
            try
            {
                await TeamRealtimeService.StartAsync(cancellationToken);
                await TeamRealtimeService.JoinTeamsAsync(
                    CaptainedTeams
                        .Concat(_teamSummary.MemberTeams)
                    .Select(team => team.Id),
                    cancellationToken);
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch(Exception)
            {
                // Registration remains usable through its normal load and mutation paths.
            }

            if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                return;

            var managedTeamId = (_registrationState?.CaptainManagedRegistrations ?? [])
                .Select(registration => registration.Team?.Id)
                .FirstOrDefault(id => id.HasValue && RegistrableCaptainedTeams.Any(team => team.Id == id.Value));

            if(draftTeamId.HasValue && RegistrableCaptainedTeams.Any(team => team.Id == draftTeamId.Value))
                _selectedTeamId = draftTeamId;
            else if(managedTeamId.HasValue)
                _selectedTeamId = managedTeamId;
            else if(_selectedTeamId.HasValue && RegistrableCaptainedTeams.All(team => team.Id != _selectedTeamId.Value))
                _selectedTeamId = null;

            _selectedTeamId ??= RegistrableCaptainedTeams
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()?.Id;

            if(_selectedTeamId.HasValue)
                await LoadSelectedTeamEligibilityAsync(
                    tournamentId,
                    generation,
                    draftTeamId == _selectedTeamId,
                    draftRosterUserIds,
                    draftStep,
                    cancellationToken);
            else
            {
                _selectedRosterUserIds.Clear();
                _hasDirtyRosterDraft = false;
                _activeTeamStep = 0;
            }
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
        }
        catch(Exception exception) when(IsUnauthorized(exception))
        {
            if(IsCurrentRequest(tournamentId, generation))
            {
                _teamSummaryUnavailable = true;
                _teamError = Localization["Feature.tournaments.teamCaptainSignIn"];
            }
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, generation))
            {
                _teamSummaryUnavailable = true;
                _teamError = GetLocalizedErrorMessage(exception, "Feature.tournaments.teamRegistrationOptionsUnavailable");
            }
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, generation))
                _isLoadingTeams = false;
        }
    }

    private async Task LoadSelectedTeamEligibilityAsync(
        Guid tournamentId,
        long generation,
        bool preserveRosterDraft = false,
        HashSet<Guid>? draftRosterUserIds = null,
        int draftStep = 0,
        CancellationToken cancellationToken = default)
    {
        if(!IsCurrentRequest(tournamentId, generation))
            return;

        cancellationToken = cancellationToken == default
            ? _registrationLoadCancellation?.Token ?? CancellationToken.None
            : cancellationToken;

        var keepDraft = preserveRosterDraft && draftRosterUserIds is not null;
        _selectedTeamEligibility = null;
        _teamEligibilityUnavailable = false;
        _rosterEligibility = null;
        _rosterEligibilityUnavailable = false;
        _rosterCandidatesById.Clear();
        if(keepDraft)
        {
            _selectedRosterUserIds.Clear();
            _selectedRosterUserIds.UnionWith(draftRosterUserIds!);
            _activeTeamStep = Math.Clamp(draftStep, 0, 2);
        }
        else
        {
            _selectedRosterUserIds.Clear();
            _activeTeamStep = 0;
        }

        if(SelectedTeam is null)
            return;

        try
        {
            _teamError = null;
            if(!_teamEligibilityById.TryGetValue(SelectedTeam.Id, out var teamEligibility))
            {
                teamEligibility = await TournamentService.CheckTeamTournamentRegistrationEligibilityAsync(
                    tournamentId,
                    SelectedTeam.Id,
                    cancellationToken);
                if(!cancellationToken.IsCancellationRequested && IsCurrentRequest(tournamentId, generation))
                    _teamEligibilityById[SelectedTeam.Id] = teamEligibility;
            }

            if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                return;

            _selectedTeamEligibility = teamEligibility;
            if(!keepDraft && CaptainManagedRegistration is { } registration)
            {
                var savedRosterMembers = registration.RosterMembers ?? [];
                foreach(var member in savedRosterMembers)
                {
                    if(member.User is not null)
                        _selectedRosterUserIds.Add(member.User.Id);
                }
            }
            else if(!keepDraft)
            {
                var teamMembers = SelectedTeam.Members ?? [];
                if(teamMembers.Any(member => member.Id == SelectedTeam.CaptainUserId))
                    _selectedRosterUserIds.Add(SelectedTeam.CaptainUserId);
            }

            if((keepDraft || CaptainManagedRegistration is not null) && SelectedTeam.CaptainUserId != Guid.Empty)
            {
                var selectedBeforeCaptainReconciliation = _selectedRosterUserIds.ToHashSet();
                var reconciledRoster = ReconcileRosterForCurrentCaptain(
                    _selectedRosterUserIds,
                    SelectedTeam.CaptainUserId);

                _selectedRosterUserIds.Clear();
                _selectedRosterUserIds.UnionWith(reconciledRoster);
                if(!selectedBeforeCaptainReconciliation.SetEquals(_selectedRosterUserIds))
                    _registrationWarning = GetLocalizedCaptainTransferWarning(
                        _selectedRosterUserIds.Count,
                        RequiredTeamSize);
            }

            await RefreshRosterEligibilityAsync(
                tournamentId,
                generation,
                includeCandidateReasons: true,
                autofillEligibleMembers: !keepDraft && CaptainManagedRegistration is null,
                cancellationToken: cancellationToken);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
        }
        catch(Exception exception) when(IsUnauthorized(exception))
        {
            if(IsCurrentRequest(tournamentId, generation))
            {
                _teamEligibilityUnavailable = true;
                _teamError = Localization["Feature.tournaments.teamRegistrationUnauthorized"];
            }
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, generation))
            {
                _teamEligibilityUnavailable = true;
                _teamError = GetLocalizedErrorMessage(exception, "Feature.tournaments.teamEligibilityUnavailable");
            }
        }
    }

    private async Task RefreshRosterEligibilityAsync(
        Guid tournamentId,
        long generation,
        bool includeCandidateReasons = false,
        bool autofillEligibleMembers = false,
        bool presentProgress = true,
        CancellationToken cancellationToken = default)
    {
        if(!IsCurrentRequest(tournamentId, generation))
            return;

        cancellationToken = cancellationToken == default
            ? _registrationLoadCancellation?.Token ?? CancellationToken.None
            : cancellationToken;

        _teamError = null;
        // A background revalidation keeps the current roster presentation on screen so the
        // focused action is neither disabled nor repainted while the check is running.
        if(presentProgress)
            _rosterEligibility = null;
        if(SelectedTeam is null)
            return;

        _rosterEligibilityUnavailable = false;
        if(presentProgress)
        {
            _isLoadingRoster = true;
            await InvokeAsync(StateHasChanged);
        }
        try
        {
            var selectedUserIds = _selectedRosterUserIds.ToArray();
            var eligibility = await TournamentService.CheckTeamRosterEligibilityAsync(
                tournamentId,
                SelectedTeam.Id,
                new SubmitTeamRosterDTO
                {
                    TeamId = SelectedTeam.Id,
                    UserIds = selectedUserIds
                },
                cancellationToken);

            if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                return;

            _rosterEligibility = eligibility;
            MergeRosterCandidateEligibility(eligibility.Candidates);

            if(includeCandidateReasons)
            {
                var candidateUserIds = EditableRosterCandidates
                    .Select(member => member.Id)
                    .Distinct()
                    .ToArray();
                if(!candidateUserIds.ToHashSet().SetEquals(selectedUserIds))
                {
                    foreach(var candidateUserIdBatch in BatchRosterEligibilityUserIds(candidateUserIds))
                    {
                        var candidateEligibility = await TournamentService.CheckTeamRosterEligibilityAsync(
                            tournamentId,
                            SelectedTeam.Id,
                            new SubmitTeamRosterDTO
                            {
                                TeamId = SelectedTeam.Id,
                                UserIds = candidateUserIdBatch
                            },
                            cancellationToken);
                        if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                            return;

                        MergeRosterCandidateEligibility(candidateEligibility.Candidates);
                    }
                }
            }

            if(autofillEligibleMembers)
            {
                var selectedBeforeAutofill = _selectedRosterUserIds.ToHashSet();
                NormalizeSelectedRoster(autofillEligibleMembers: true);
                if(!selectedBeforeAutofill.SetEquals(_selectedRosterUserIds))
                {
                    var autofilledEligibility = await TournamentService.CheckTeamRosterEligibilityAsync(
                        tournamentId,
                        SelectedTeam.Id,
                        BuildRosterRequest(),
                        cancellationToken);
                    if(cancellationToken.IsCancellationRequested || !IsCurrentRequest(tournamentId, generation))
                        return;

                    _rosterEligibility = autofilledEligibility;
                    MergeRosterCandidateEligibility(autofilledEligibility.Candidates);
                }
            }
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
        }
        catch(Exception exception) when(IsUnauthorized(exception))
        {
            if(IsCurrentRequest(tournamentId, generation))
            {
                _rosterEligibilityUnavailable = true;
                _teamError = Localization["Feature.tournaments.rosterValidationUnauthorized"];
            }
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, generation))
            {
                _rosterEligibilityUnavailable = true;
                _teamError = GetLocalizedErrorMessage(exception, "Feature.tournaments.rosterEligibilityUnavailable");
            }
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, generation))
                _isLoadingRoster = false;
        }
    }

    private async Task LoadAdminRegistrationsAsync(
        Guid tournamentId,
        long generation,
        CancellationToken cancellationToken)
    {
        _isLoadingAdmin = true;
        try
        {
            var registrations = await TournamentService.GetAdminTournamentRegistrationsAsync(
                tournamentId,
                1,
                100,
                cancellationToken);
            if(!cancellationToken.IsCancellationRequested && IsCurrentRequest(tournamentId, generation))
            {
                _adminRegistrations.Clear();
                _adminRegistrations.AddRange(registrations);
            }
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
        }
        catch(Exception exception) when(IsUnauthorized(exception))
        {
            if(IsCurrentRequest(tournamentId, generation))
                _adminError = Localization["Feature.tournaments.adminRegistrationUnauthorized"];
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, generation))
                _adminError = GetLocalizedErrorMessage(exception, "Feature.tournaments.adminRegistrationUnavailable");
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, generation))
                _isLoadingAdmin = false;
        }
    }

    private string GetRegistrationActionLabel() =>
        _registrationState?.IndividualRegistration is not null ||
        HasCaptainManagedRegistration ||
        CurrentTeamRegistration is not null
            ? Localization["Feature.tournaments.manageRegistration"]
            : Localization["Feature.tournaments.viewRegistrationOptions"];

    private async Task OpenRegistrationDialog()
    {
        if(!IsRegistrationOpen || _isSubmitting)
            return;

        // An eligible individual player has nothing left to choose, so the next
        // interaction is the confirmation instead of a popup that repeats the action.
        if(CanRegisterIndividualDirectly)
        {
            _isIndividualRegistrationConfirmationOpen = true;
            _registrationError = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if(!_registrationLoadCompleted)
            _registrationLoadRequested = true;

        _isRegistrationDialogOpen = true;
        await RegistrationDialogOpenChanged.InvokeAsync(true);
    }

    private async Task CloseRegistrationDialogAsync()
    {
        _isRegistrationDialogOpen = false;
        _isTeamUnregistrationConfirmationOpen = false;
        _teamUnregistrationConfirmationGeneration = null;
        _teamUnregistrationConfirmationTeamId = null;
        _isIndividualRegistrationConfirmationOpen = false;
        _isIndividualUnregistrationConfirmationOpen = false;
        _focusTeamUnregistrationConfirmation = false;
        _restoreTeamUnregistrationFocus = false;
        if(!_registrationLoadCompleted || _registrationLoadCancellation is not null)
        {
            _requestGeneration++;
            _hasLoadedForTournament = false;
            _registrationLoadCompleted = false;
            _registrationLoadRequested = false;
            CancelRegistrationLoad();
        }
        await DisposeRegistrationFocusTrapAsync();
        await RegistrationDialogOpenChanged.InvokeAsync(false);
    }

    private async Task HandleRegistrationDialogKeyDown(KeyboardEventArgs args)
    {
        if(!string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            return;

        if(_isTeamUnregistrationConfirmationOpen)
        {
            await KeepTeamRegistrationAsync();
            return;
        }

        if(_isIndividualUnregistrationConfirmationOpen)
        {
            await DismissIndividualUnregistrationConfirmationAsync();
            return;
        }

        if(!_isSubmitting)
            await CloseRegistrationDialogAsync();
    }

    private async Task HandleTeamUnregistrationConfirmationKeyDown(KeyboardEventArgs args)
    {
        if(string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            await KeepTeamRegistrationAsync();
    }

    private async Task RegisterIndividualAsync()
    {
        if(_isSubmitting ||
           _isLoadingRegistration ||
           !CanRegisterIndividualDirectly)
            return;

        var tournamentId = Tournament.Id;
        var requestGeneration = ++_requestGeneration;

        if(!await RevalidateIndividualRegistrationAsync(registering: true, requestGeneration))
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
                _isIndividualRegistrationConfirmationOpen = false;
            return;
        }

        if(!IsCurrentRequest(tournamentId, requestGeneration))
            return;

        var registered = await RunRegistrationActionAsync(
            () => TournamentService.RegisterCurrentUserForTournamentAsync(tournamentId),
            Localization["Feature.tournaments.individualRegistered"],
            tournamentId,
            requestGeneration);

        if(!IsCurrentRequest(tournamentId, requestGeneration) || _isDisposed)
            return;

        if(!registered)
            return;

        // The registration reached the backend, so the confirmation is answered and a
        // still-open popup closes itself instead of asking for an extra dismissal.
        _isIndividualRegistrationConfirmationOpen = false;
        if(_isRegistrationDialogOpen)
            await CloseRegistrationDialogAsync();
        else
            await InvokeAsync(StateHasChanged);
    }

    private async Task UnregisterIndividualAsync()
    {
        if(_isSubmitting ||
           _isLoadingRegistration ||
           !CanUnregisterIndividualDirectly)
            return;

        var tournamentId = Tournament.Id;
        var requestGeneration = ++_requestGeneration;

        if(!await RevalidateIndividualRegistrationAsync(registering: false, requestGeneration))
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
                _isIndividualUnregistrationConfirmationOpen = false;
            return;
        }

        if(!IsCurrentRequest(tournamentId, requestGeneration))
            return;

        await RunRegistrationActionAsync(
            () => TournamentService.DeleteCurrentUserTournamentRegistrationAsync(tournamentId),
            Localization["Feature.tournaments.individualUnregistered"],
            tournamentId,
            requestGeneration);

        _isIndividualUnregistrationConfirmationOpen = false;
    }

    private async Task DismissIndividualRegistrationConfirmationAsync()
    {
        if(!_isIndividualRegistrationConfirmationOpen)
            return;

        _isIndividualRegistrationConfirmationOpen = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task BeginIndividualUnregistrationConfirmationAsync()
    {
        if(_isSubmitting || _isLoadingRegistration || !CanUnregisterIndividualDirectly)
            return;

        _isIndividualUnregistrationConfirmationOpen = true;
        _registrationError = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task DismissIndividualUnregistrationConfirmationAsync()
    {
        if(!_isIndividualUnregistrationConfirmationOpen)
            return;

        _isIndividualUnregistrationConfirmationOpen = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ConfirmRosterMemberAsync()
    {
        if(!CanConfirmPending || _isSubmitting)
            return;

        var tournamentId = Tournament.Id;
        var requestGeneration = ++_requestGeneration;

        if(!await ReloadCurrentUserStateAsync(requestGeneration))
            return;

        var pending = _registrationState?.PendingRosterConfirmation;
        if(!CanConfirmPending || pending is null)
            return;

        if(!IsCurrentRequest(tournamentId, requestGeneration))
            return;

        var accepted = await RunRegistrationActionAsync(
            () => TournamentService.ConfirmTournamentRosterMemberAsync(tournamentId, pending.Id),
            Localization["Feature.tournaments.rosterPlaceConfirmed"],
            tournamentId,
            requestGeneration);

        if(accepted)
            await RefreshNotificationsAfterRosterDecisionAsync();
    }

    private async Task DeclineRosterMemberAsync()
    {
        if(!CanConfirmPending || _isSubmitting)
            return;

        var tournamentId = Tournament.Id;
        var requestGeneration = ++_requestGeneration;
        if(!await ReloadCurrentUserStateAsync(requestGeneration))
            return;

        var pending = _registrationState?.PendingRosterConfirmation;
        if(!CanConfirmPending || pending is null || !IsCurrentRequest(tournamentId, requestGeneration))
            return;

        var declined = await RunRegistrationActionAsync(
            () => TournamentService.DeclineTournamentRosterMemberAsync(tournamentId, pending.Id),
            Localization["Feature.tournaments.rosterPlaceDeclined"],
            tournamentId,
            requestGeneration);

        if(declined)
            await RefreshNotificationsAfterRosterDecisionAsync();
    }

    private async Task RefreshNotificationsAfterRosterDecisionAsync()
    {
        try
        {
            await NotificationService.RefreshAsync();
        }
        catch(Exception)
        {
            // The registration surface already refreshed its authoritative state.
        }
    }

    private async Task SubmitTeamRosterAsync()
    {
        if(!CanSubmitRoster || _isSubmitting || SelectedTeam is null)
            return;

        var teamId = SelectedTeam.Id;
        var tournamentId = Tournament.Id;
        var requestGeneration = ++_requestGeneration;
        var request = BuildRosterRequest();
        if(!await RevalidateRosterBeforeSubmitAsync(teamId, request, requestGeneration))
            return;

        if(!IsCurrentRequest(tournamentId, requestGeneration) || !CanSubmitRoster || SelectedTeam?.Id != teamId)
            return;

        await RunRegistrationActionAsync(
            () => TournamentService.SubmitTeamTournamentRosterAsync(tournamentId, teamId, request),
            HasCaptainManagedRegistration
                ? Localization["Feature.tournaments.rosterChangesSaved"]
                : Localization["Feature.tournaments.rosterSubmitted"],
            tournamentId,
            requestGeneration);
    }

    private async Task BeginDirectTeamUnregistrationAsync()
    {
        if(!CanCancelTeamRegistrationDirectly || CurrentCaptainManagedRegistration?.Team is not { } team)
            return;

        await BeginTeamUnregistrationConfirmationAsync(team.Id);
    }

    private async Task BeginTeamUnregistrationConfirmationAsync(Guid teamId)
    {
        if(!CanUnregisterTeam(teamId))
            return;

        _teamUnregistrationConfirmationTeamId = teamId;
        _teamUnregistrationConfirmationGeneration = ++_requestGeneration;
        _isTeamUnregistrationConfirmationOpen = true;
        _registrationError = null;
        _teamError = null;
        _focusTeamUnregistrationConfirmation = true;
        _restoreTeamUnregistrationFocus = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task KeepTeamRegistrationAsync()
    {
        if(!_isTeamUnregistrationConfirmationOpen)
            return;

        _isTeamUnregistrationConfirmationOpen = false;
        _teamUnregistrationConfirmationGeneration = null;
        _teamUnregistrationConfirmationTeamId = null;
        _focusTeamUnregistrationConfirmation = false;
        _restoreTeamUnregistrationFocus = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task UnregisterTeamAsync()
    {
        if(_teamUnregistrationConfirmationTeamId is not { } teamId ||
           !CanUnregisterTeam(teamId) ||
           !_isTeamUnregistrationConfirmationOpen ||
           !_teamUnregistrationConfirmationGeneration.HasValue)
            return;

        _teamUnregistrationConfirmationTeamId = null;
        var tournamentId = Tournament.Id;
        var requestGeneration = _teamUnregistrationConfirmationGeneration.Value;
        _isTeamUnregistrationConfirmationOpen = false;
        _teamUnregistrationConfirmationGeneration = null;

        if(!IsCurrentRequest(tournamentId, requestGeneration))
            return;

        if(!await ReloadCurrentUserStateAsync(requestGeneration))
            return;

        var latestRegistration = (_registrationState?.CaptainManagedRegistrations ?? [])
            .FirstOrDefault(registration => registration.Team?.Id == teamId);
        if(!IsCurrentRequest(tournamentId, requestGeneration) ||
           !IsRegistrationOpen ||
           _registrationState?.CanUnregister != true ||
           latestRegistration is null)
            return;

        await RunRegistrationActionAsync(
            () => TournamentService.DeleteTeamTournamentRegistrationAsync(tournamentId, teamId),
            Localization["Feature.tournaments.teamUnregistered"],
            tournamentId,
            requestGeneration);
    }

    private async Task<bool> RunRegistrationActionAsync(
        Func<Task> action,
        string successMessage,
        Guid tournamentId,
        long requestGeneration)
    {
        if(!IsCurrentRequest(tournamentId, requestGeneration))
            return false;

        _isSubmitting = true;
        _registrationError = null;
        try
        {
            try
            {
                if(!IsCurrentRequest(tournamentId, requestGeneration))
                    return false;

                await action();
                if(!IsCurrentRequest(tournamentId, requestGeneration))
                    return false;

                ToastService.ShowSuccess(successMessage);
            }
            catch(Exception exception) when(IsUnauthorized(exception))
            {
                if(IsCurrentRequest(tournamentId, requestGeneration))
                {
                    _registrationError = Localization["Feature.tournaments.registrationChangeUnauthorized"];
                    ToastService.ShowError(_registrationError);
                }
                return false;
            }
            catch(Exception exception)
            {
                if(IsCurrentRequest(tournamentId, requestGeneration))
                {
                    _registrationError = GetLocalizedErrorMessage(exception, "Feature.tournaments.registrationChangeFailed");
                    ToastService.ShowError(_registrationError);
                }
                return false;
            }

            try
            {
                var currentUserStateLoaded = await RefreshTournamentAsync(
                    tournamentId,
                    expectedRequestGeneration: requestGeneration,
                    preserveSubmitting: true);

                if(ShouldReportSavedMutationRefreshFailure(
                       currentUserStateLoaded,
                       IsCurrentTournament(tournamentId)))
                {
                    if(IsCurrentRequest(tournamentId, requestGeneration))
                    {
                        _registrationError =
                            Localization["Feature.tournaments.savedDetailsUnavailable"];
                        ToastService.ShowWarning(_registrationError);
                    }
                }
            }
            catch(Exception exception)
            {
                if(IsCurrentRequest(tournamentId, requestGeneration))
                {
                    _registrationError = Localization.Get(
                        "Feature.tournaments.savedDetailsUnavailableWithReason",
                        GetLocalizedErrorMessage(exception, "Feature.tournaments.loadingFailed"));
                    ToastService.ShowWarning(_registrationError);
                }
            }

            return true;
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
            {
                _isSubmitting = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task RemoveAdminRegistrationAsync(AdminTournamentRegistrationDTO registration)
    {
        if(_pendingAdminRemovalRegistrationId.HasValue || _isLoadingRegistration || _isSubmitting)
            return;

        _pendingAdminRemovalRegistrationId = registration.Id;
        _adminError = null;
        var tournamentId = Tournament.Id;
        var requestGeneration = ++_requestGeneration;
        _isSubmitting = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            try
            {
                if(!IsCurrentRequest(tournamentId, requestGeneration))
                    return;

                if(registration.Kind == TournamentRegistrationKind.Individual && registration.User is not null)
                {
                    await TournamentService.RemoveTournamentUserRegistrationAsAdminAsync(
                        tournamentId,
                        registration.User.Id,
                        _adminRemovalReason);
                }
                else if(registration.Kind == TournamentRegistrationKind.Team && registration.Team is not null)
                {
                    await TournamentService.RemoveTournamentTeamRegistrationAsAdminAsync(
                        tournamentId,
                        registration.Team.Id,
                        _adminRemovalReason);
                }
                else
                {
                    if(IsCurrentRequest(tournamentId, requestGeneration))
                        _adminError = Localization["Feature.tournaments.registrationIdentityMissing"];
                    return;
                }

                if(IsCurrentRequest(tournamentId, requestGeneration))
                {
                    _adminRemovalReason = string.Empty;
                    ToastService.ShowSuccess(Localization["Feature.tournaments.registrationRemoved"]);
                }
            }
            catch(Exception exception) when(IsUnauthorized(exception))
            {
                if(IsCurrentRequest(tournamentId, requestGeneration))
                {
                    _adminError = Localization["Feature.tournaments.registrationRemoveUnauthorized"];
                    ToastService.ShowError(_adminError);
                }
                return;
            }
            catch(Exception exception)
            {
                if(IsCurrentRequest(tournamentId, requestGeneration))
                {
                    _adminError = GetLocalizedErrorMessage(exception, "Feature.tournaments.registrationRemoveFailed");
                    ToastService.ShowError(_adminError);
                }
                return;
            }

            try
            {
                await RefreshTournamentAsync(
                    tournamentId,
                    expectedRequestGeneration: requestGeneration,
                    preserveSubmitting: true);
            }
            catch(Exception exception)
            {
                if(IsCurrentRequest(tournamentId, requestGeneration))
                {
                    _adminError = Localization.Get(
                        "Feature.tournaments.registrationRemovedListUnavailable",
                        GetLocalizedErrorMessage(exception, "Feature.tournaments.loadingFailed"));
                    ToastService.ShowWarning(_adminError);
                }
            }
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
            {
                _pendingAdminRemovalRegistrationId = null;
                _isSubmitting = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task SelectTeamAsync(Guid teamId)
    {
        if(_isSubmitting || _isLoadingRegistration || _isLoadingTeams || _isLoadingRoster || !CanSelectTeam(teamId))
            return;

        if(_selectedTeamId == teamId && _selectedTeamEligibility is not null)
            return;

        _hasDirtyRosterDraft = false;
        _selectedTeamId = teamId;
        var generation = ++_requestGeneration;
        var cancellation = BeginRegistrationLoad();
        _isLoadingRegistration = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            await LoadSelectedTeamEligibilityAsync(
                Tournament.Id,
                generation,
                cancellationToken: cancellation.Token);
        }
        finally
        {
            if(IsCurrentRequest(Tournament.Id, generation))
            {
                _isLoadingRegistration = false;
                await InvokeAsync(StateHasChanged);
            }
            EndRegistrationLoad(cancellation);
        }
    }

    private async Task HandleTeamChangedAsync(ChangeEventArgs args)
    {
        if(Guid.TryParse(args.Value?.ToString(), out var id))
            await SelectTeamAsync(id);
    }

    private async Task ToggleRosterMemberAsync(Guid userId, ChangeEventArgs args)
    {
        NormalizeSelectedRoster();
        if(_isSubmitting ||
           _isLoadingRegistration ||
           _isLoadingRoster ||
           SelectedTeam?.CaptainUserId == userId ||
           !CanSelectRosterMember(userId))
            return;

        if(args.Value is bool selected && selected)
            _selectedRosterUserIds.Add(userId);
        else
            _selectedRosterUserIds.Remove(userId);

        _hasDirtyRosterDraft = true;
        var generation = ++_requestGeneration;
        var cancellation = BeginRegistrationLoad();
        try
        {
            await RefreshRosterEligibilityAsync(
                Tournament.Id,
                generation,
                includeCandidateReasons: true,
                cancellationToken: cancellation.Token);
        }
        finally
        {
            EndRegistrationLoad(cancellation);
        }
    }

    private async Task NextTeamStepAsync()
    {
        if(_isSubmitting || _isLoadingRegistration || _isLoadingRoster)
            return;

        var requestGeneration = _requestGeneration;
        if(_activeTeamStep == 0)
        {
            if(!CanAdvanceFromTeamSelection)
                return;
        }
        else if(_activeTeamStep == 1)
        {
            if(!HasLocalRosterShape)
            {
                _teamError = Localization.Get("Feature.tournaments.selectExactRoster", RequiredTeamSize);
                return;
            }

            var request = BuildRosterRequest();
            requestGeneration = ++_requestGeneration;
            if(SelectedTeam is null || !await RevalidateRosterBeforeSubmitAsync(SelectedTeam.Id, request, requestGeneration))
                return;
        }
        else
        {
            return;
        }

        if(!IsCurrentRequest(Tournament.Id, requestGeneration))
            return;

        _activeTeamStep++;
    }

    private async Task HandleTeamStepPreviewAsync(StepperInteractionEventArgs args)
    {
        if(args.Action != StepAction.Activate)
            return;

        var isBusy = _isSubmitting || _isLoadingRegistration || _isLoadingRoster;
        if(!CanNavigateTeamStep(_activeTeamStep, args.StepIndex, isBusy, CanAdvanceTeamStep))
        {
            args.Cancel = true;
            return;
        }

        if(args.StepIndex <= _activeTeamStep)
            return;

        if(_activeTeamStep == 1)
        {
            args.Cancel = true;
            if(!HasLocalRosterShape)
            {
                _teamError = Localization.Get("Feature.tournaments.selectExactRoster", RequiredTeamSize);
                return;
            }

            if(SelectedTeam is not { } team)
                return;

            var requestGeneration = ++_requestGeneration;
            var request = BuildRosterRequest();
            args.Cancel = !await RevalidateRosterBeforeSubmitAsync(team.Id, request, requestGeneration);
            return;
        }

        args.Cancel = false;
    }

    private Task HandleTeamStepChangedAsync(int index)
    {
        if(CanNavigateTeamStep(
               _activeTeamStep,
               index,
               _isSubmitting || _isLoadingRegistration || _isLoadingRoster,
               CanAdvanceTeamStep))
            _activeTeamStep = index;

        return Task.CompletedTask;
    }

    internal static bool CanNavigateTeamStep(
        int currentStep,
        int requestedStep,
        bool isBusy,
        bool canAdvanceCurrentStep) =>
        !isBusy &&
        requestedStep is >= 0 and <= 2 &&
        (requestedStep <= currentStep ||
         requestedStep == currentStep + 1 && canAdvanceCurrentStep);

    private SubmitTeamRosterDTO BuildRosterRequest() => new()
    {
        TeamId = SelectedTeam?.Id ?? Guid.Empty,
        UserIds = GetNormalizedRosterSelection().ToArray()
    };

    private async Task<bool> RevalidateRosterBeforeSubmitAsync(
        Guid teamId,
        SubmitTeamRosterDTO request,
        long requestGeneration)
    {
        var cancellation = BeginRegistrationLoad();
        try
        {
            await RefreshRosterEligibilityAsync(
                Tournament.Id,
                requestGeneration,
                presentProgress: false,
                cancellationToken: cancellation.Token);

            var isCurrent = !cancellation.IsCancellationRequested &&
                            IsCurrentRequest(Tournament.Id, requestGeneration);
            var isEligible = isCurrent &&
                             !_rosterEligibilityUnavailable &&
                             SelectedTeam?.Id == teamId &&
                             IsRosterEligibleForWorkflow &&
                             request.UserIds.ToHashSet().SetEquals(_selectedRosterUserIds);
            if(!isEligible && isCurrent && string.IsNullOrEmpty(_teamError))
                _teamError = Localization["Feature.tournaments.rosterMemberIneligible"];

            return isEligible;
        }
        finally
        {
            EndRegistrationLoad(cancellation);
        }
    }

    private async Task<bool> RevalidateIndividualRegistrationAsync(bool registering, long requestGeneration)
    {
        var tournamentId = Tournament.Id;
        var cancellation = BeginRegistrationLoad();
        _isLoadingRegistration = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            var state = await TournamentService.GetCurrentUserTournamentRegistrationStateAsync(
                tournamentId,
                cancellation.Token);
            var eligibility = await TournamentService.CheckIndividualTournamentRegistrationEligibilityAsync(
                tournamentId,
                cancellation.Token);
            if(cancellation.IsCancellationRequested || !IsCurrentRequest(tournamentId, requestGeneration))
                return false;

            _registrationState = state;
            _individualEligibility = eligibility;
            return IsRegistrationOpen && (registering
                ? state.CanRegisterIndividual && eligibility.Eligible && state.IndividualRegistration is null
                : state.CanUnregister && state.IndividualRegistration is not null);
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested)
        {
        }
        catch(Exception exception) when(IsUnauthorized(exception))
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
                _registrationError = Localization["Feature.tournaments.registrationManageUnauthorized"];
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
                _registrationError = GetLocalizedErrorMessage(exception, "Feature.tournaments.registrationOptionsUnavailable");
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
            {
                _isLoadingRegistration = false;
                await InvokeAsync(StateHasChanged);
            }
            EndRegistrationLoad(cancellation);
        }

        return false;
    }

    private async Task<bool> ReloadCurrentUserStateAsync(long requestGeneration)
    {
        var tournamentId = Tournament.Id;
        var cancellation = BeginRegistrationLoad();
        _isLoadingRegistration = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            var state = await TournamentService.GetCurrentUserTournamentRegistrationStateAsync(
                tournamentId,
                cancellation.Token);
            if(cancellation.IsCancellationRequested || !IsCurrentRequest(tournamentId, requestGeneration))
                return false;

            _registrationState = state;
            return true;
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested)
        {
        }
        catch(Exception exception) when(IsUnauthorized(exception))
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
                _registrationError = Localization["Feature.tournaments.registrationManageUnauthorized"];
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
                _registrationError = GetLocalizedErrorMessage(exception, "Feature.tournaments.registrationOptionsUnavailable");
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
            {
                _isLoadingRegistration = false;
                await InvokeAsync(StateHasChanged);
            }
            EndRegistrationLoad(cancellation);
        }

        return false;
    }

    private async Task HandleTeamStateInvalidatedAsync()
    {
        if(_isDisposed)
            return;

        await InvokeAsync(() => _teamStateInvalidationGate.RunAsync(RefreshAfterTeamInvalidationAsync));
    }

    private async Task HandleRosterDecisionChangedAsync(Guid tournamentId)
    {
        if(_isDisposed ||
           !_isAuthenticated ||
           tournamentId != Tournament.Id ||
           (PopupOnly && !_isRegistrationDialogOpen))
            return;

        await InvokeAsync(() => _teamStateInvalidationGate.RunAsync(
            () => RefreshAfterRosterDecisionAsync(tournamentId)));
    }

    private async Task RefreshAfterRosterDecisionAsync(Guid tournamentId)
    {
        if(_isDisposed ||
           !_isAuthenticated ||
           tournamentId != Tournament.Id ||
           _isSubmitting)
            return;

        var requestGeneration = ++_requestGeneration;
        await ReloadCurrentUserStateAsync(requestGeneration);
    }

    private async Task RefreshAfterTeamInvalidationAsync()
    {
        if(_isDisposed ||
           !_isAuthenticated ||
           _isSubmitting ||
           Tournament.ParticipationMode != ParticipationMode.Team)
            return;

        var tournamentId = Tournament.Id;
        var requestGeneration = ++_requestGeneration;
        var preserveRosterDraft = HasDirtyRosterDraft;
        _isLoadingRegistration = true;
        _registrationError = null;
        await InvokeAsync(StateHasChanged);
        try
        {
            await RefreshTournamentAsync(
                tournamentId,
                preserveRosterDraft,
                requestGeneration);
        }
        catch(Exception exception)
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
                _teamError = GetLocalizedErrorMessage(exception, "Feature.tournaments.teamOptionsUnavailable");
        }
        finally
        {
            if(IsCurrentRequest(tournamentId, requestGeneration))
            {
                _isLoadingRegistration = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task<bool> RefreshTournamentAsync(
        Guid tournamentId,
        bool preserveRosterDraft = false,
        long? expectedRequestGeneration = null,
        bool preserveSubmitting = false)
    {
        var requestGeneration = expectedRequestGeneration ?? _requestGeneration;
        var cancellation = BeginRegistrationLoad();
        try
        {
            var updatedTournament = await TournamentService.GetTournamentByIdAsync(
                tournamentId,
                cancellation.Token);
            if(cancellation.IsCancellationRequested || !IsCurrentRequest(tournamentId, requestGeneration))
                return false;

            if(updatedTournament is null)
                throw new InvalidOperationException(Localization["Feature.tournaments.tournamentNotFoundLoadingRegistration"]);

            Tournament = updatedTournament;
            _loadedRegistrationFingerprint = GetRegistrationFingerprint(updatedTournament);
            _participants.Clear();
            _participants.AddRange(BuildParticipants(updatedTournament));

            if(cancellation.IsCancellationRequested || !IsCurrentRequest(tournamentId, requestGeneration))
                return false;

            await OnTournamentUpdated.InvokeAsync(updatedTournament);

            if(IsCurrentRequest(tournamentId, requestGeneration))
                return await LoadRegistrationContextAsync(
                    preserveRosterDraft,
                    preserveSubmitting,
                    preserveSubmitting ? requestGeneration : null,
                    cancellation.Token);

            return false;
        }
        finally
        {
            EndRegistrationLoad(cancellation);
        }
    }

    internal static bool ShouldReportSavedMutationRefreshFailure(bool currentUserStateLoaded, bool isCurrentRoute) =>
        isCurrentRoute && !currentUserStateLoaded;

    internal static bool ShouldPreserveRosterDraftAfterRefresh(Guid? draftTeamId, Guid? selectedTeamId) =>
        draftTeamId.HasValue && draftTeamId == selectedTeamId;

    internal static IReadOnlyList<IReadOnlyList<Guid>> BatchRosterEligibilityUserIds(IEnumerable<Guid> userIds)
    {
        var batches = new List<IReadOnlyList<Guid>>();
        var batch = new List<Guid>(MaxRosterEligibilityUserIds);

        foreach(var userId in userIds.Distinct())
        {
            batch.Add(userId);
            if(batch.Count < MaxRosterEligibilityUserIds)
                continue;

            batches.Add(batch);
            batch = new List<Guid>(MaxRosterEligibilityUserIds);
        }

        if(batch.Count > 0)
            batches.Add(batch);

        return batches;
    }

    internal static IReadOnlyList<Guid> ReconcileRosterForCurrentCaptain(
        IEnumerable<Guid> selectedUserIds,
        Guid captainUserId)
    {
        var reconciled = selectedUserIds.Distinct().ToList();
        if(captainUserId == Guid.Empty)
            return reconciled;

        if(!reconciled.Contains(captainUserId))
            reconciled.Add(captainUserId);

        return reconciled;
    }

    internal static IReadOnlyList<TeamManagementSummaryDTO> FilterCaptainedTeamsByRequiredSize(
        IEnumerable<TeamManagementSummaryDTO> teams,
        int requiredTeamSize) =>
        requiredTeamSize > 0
            ? teams.Where(team => team.Members.Count >= requiredTeamSize).ToArray()
            : [];

    internal static TournamentRegistrationDTO? FindCaptainManagedRegistration(
        IEnumerable<TournamentRegistrationDTO> registrations,
        IEnumerable<TeamManagementSummaryDTO> captainedTeams) =>
        registrations.FirstOrDefault(registration =>
            registration.Team is { } team &&
            captainedTeams.Any(captainedTeam => captainedTeam.Id == team.Id));

    internal static Guid? GetTeamCancellationTarget(
        int activeStep,
        Guid? selectedTeamId,
        bool canUnregisterSelectedTeam,
        Guid? captainManagedTeamId,
        bool canUnregisterCaptainManagedTeam) =>
        activeStep == 2
            ? canUnregisterSelectedTeam ? selectedTeamId : null
            : canUnregisterCaptainManagedTeam ? captainManagedTeamId : null;

    internal static IReadOnlyList<Guid> MergeEditableRosterCandidateIds(
        IEnumerable<Guid> existingCandidateIds,
        IEnumerable<Guid> selectedRosterUserIds,
        IEnumerable<Guid> eligibilityCandidateIds) =>
        existingCandidateIds
            .Concat(selectedRosterUserIds)
            .Concat(eligibilityCandidateIds)
            .Distinct()
            .ToArray();

    internal static string GetCaptainTransferWarning(int rosterCount, int requiredTeamSize) =>
        rosterCount > requiredTeamSize
            ? $"The team captain changed. The current captain was added to this roster, which now has {rosterCount} of the required {requiredTeamSize} members. Choose a member to remove before saving."
            : "The team captain changed. The current captain was added to this roster. Review the roster before saving.";

    private string GetLocalizedCaptainTransferWarning(int rosterCount, int requiredTeamSize) =>
        rosterCount > requiredTeamSize
            ? Localization.Get("Feature.tournaments.captainChangedRosterTooLarge", rosterCount, requiredTeamSize)
            : Localization["Feature.tournaments.captainChangedReviewRoster"];

    private void MergeRosterCandidateEligibility(IEnumerable<RosterCandidateEligibilityDTO>? candidates)
    {
        MergeRosterCandidateEligibility(_rosterCandidatesById, candidates);
        NormalizeSelectedRoster();
    }

    internal static void MergeRosterCandidateEligibility(
        IDictionary<Guid, RosterCandidateEligibilityDTO> candidatesById,
        IEnumerable<RosterCandidateEligibilityDTO>? candidates)
    {
        foreach(var candidate in (candidates ?? []).OrderBy(candidate => candidate.UserId))
        {
            if(!candidatesById.TryGetValue(candidate.UserId, out var existing))
            {
                candidatesById[candidate.UserId] = candidate;
                continue;
            }

            candidatesById[candidate.UserId] = new RosterCandidateEligibilityDTO
            {
                UserId = candidate.UserId,
                User = existing.User ?? candidate.User,
                Eligible = existing.Eligible && candidate.Eligible,
                ReasonCodes = existing.ReasonCodes
                    .Concat(candidate.ReasonCodes)
                    .Where(reason => !string.IsNullOrWhiteSpace(reason))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(reason => reason, StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }

    private bool IsCurrentRequest(Guid tournamentId, long requestGeneration) =>
        !_isDisposed &&
        IsRequestCurrent(
            tournamentId,
            Tournament.Id,
            requestGeneration,
            _requestGeneration);

    internal static bool IsRequestCurrent(
        Guid expectedTournamentId,
        Guid currentTournamentId,
        long expectedRequestGeneration,
        long currentRequestGeneration) =>
        expectedTournamentId == currentTournamentId &&
        expectedRequestGeneration == currentRequestGeneration;

    private bool IsCurrentTournament(Guid tournamentId) =>
        !_isDisposed && Tournament.Id == tournamentId;

    private EligibilityResponseDTO? GetTeamEligibility(Guid teamId) =>
        _teamEligibilityById.TryGetValue(teamId, out var eligibility) ? eligibility : null;

    private bool CanSelectTeam(Guid teamId)
    {
        var eligibility = GetTeamEligibility(teamId);
        return eligibility is null || CanUseTeamEligibility(teamId, eligibility);
    }

    private bool CanUseTeamEligibility(Guid teamId, EligibilityResponseDTO eligibility) =>
        eligibility.Eligible ||
        (IsCurrentCaptainManagedTeam(teamId) &&
         eligibility.ReasonCodes.Count > 0 &&
         eligibility.ReasonCodes.All(IsExistingRegistrationConflictCode));

    private bool IsCurrentCaptainManagedTeam(Guid teamId) =>
        (_registrationState?.CaptainManagedRegistrations ?? [])
            .Any(registration => registration.Team?.Id == teamId);

    private bool CanSelectRosterMember(Guid userId) =>
        CanSelectRosterMember(userId, GetRosterSelectionContext());

    private bool CanSelectRosterMember(Guid userId, RosterSelectionContext context)
    {
        if(SelectedTeam is null)
            return false;

        return IsRosterMemberSelectable(
            userId,
            SelectedTeam.CaptainUserId,
            context.CurrentTeamMemberIds,
            _rosterCandidatesById,
            context.ExistingRosterUserIds);
    }

    // The roster markup needs these sets once per rendered member, so callers build the context
    // once per render and reuse it instead of re-allocating both sets for every candidate.
    private RosterSelectionContext GetRosterSelectionContext() =>
        new(
            (SelectedTeam?.Members ?? []).Select(member => member.Id).ToHashSet(),
            GetExistingRosterUserIds());

    private readonly record struct RosterSelectionContext(
        IReadOnlySet<Guid> CurrentTeamMemberIds,
        IReadOnlySet<Guid> ExistingRosterUserIds);

    private IReadOnlyList<Guid> GetNormalizedRosterSelection(bool autofillEligibleMembers = false)
    {
        if(SelectedTeam is null)
            return [];

        return NormalizeRosterSelection(
            _selectedRosterUserIds,
            (SelectedTeam.Members ?? []).Select(member => member.Id).ToArray(),
            SelectedTeam.CaptainUserId,
            _rosterCandidatesById,
            GetExistingRosterUserIds(),
            autofillEligibleMembers ? RequiredTeamSize : null);
    }

    private void NormalizeSelectedRoster(bool autofillEligibleMembers = false)
    {
        var normalized = GetNormalizedRosterSelection(autofillEligibleMembers);
        _selectedRosterUserIds.Clear();
        _selectedRosterUserIds.UnionWith(normalized);
    }

    private IReadOnlySet<Guid> GetExistingRosterUserIds() =>
        (CaptainManagedRegistration?.RosterMembers ?? [])
            .Where(member => member.User is not null)
            .Select(member => member.User.Id)
            .ToHashSet();

    internal static IReadOnlyList<Guid> NormalizeRosterSelection(
        IEnumerable<Guid> selectedUserIds,
        IReadOnlyList<Guid> currentTeamMemberIds,
        Guid captainUserId,
        IReadOnlyDictionary<Guid, RosterCandidateEligibilityDTO> candidatesById,
        IReadOnlySet<Guid> existingRosterUserIds,
        int? autofillToSize = null)
    {
        var currentTeamMembers = currentTeamMemberIds.ToHashSet();
        var normalized = selectedUserIds
            .Distinct()
            .Where(userId => IsRosterMemberSelectable(
                userId,
                captainUserId,
                currentTeamMembers,
                candidatesById,
                existingRosterUserIds))
            .ToList();

        if(captainUserId != Guid.Empty && !normalized.Contains(captainUserId))
            normalized.Insert(0, captainUserId);

        if(autofillToSize is > 0)
        {
            foreach(var userId in currentTeamMemberIds.Where(userId =>
                        normalized.Count < autofillToSize &&
                        !normalized.Contains(userId) &&
                        IsRosterMemberSelectable(
                            userId,
                            captainUserId,
                            currentTeamMembers,
                            candidatesById,
                            existingRosterUserIds)))
            {
                normalized.Add(userId);
            }
        }

        return normalized;
    }

    internal static bool IsRosterMemberSelectable(
        Guid userId,
        Guid captainUserId,
        IReadOnlySet<Guid> currentTeamMemberIds,
        IReadOnlyDictionary<Guid, RosterCandidateEligibilityDTO> candidatesById,
        IReadOnlySet<Guid> existingRosterUserIds)
    {
        if(userId == captainUserId)
            return captainUserId != Guid.Empty;

        if(!currentTeamMemberIds.Contains(userId) ||
           !candidatesById.TryGetValue(userId, out var candidate))
            return false;

        return candidate.Eligible ||
               IsExistingRosterConflictAllowed(userId, candidate, existingRosterUserIds);
    }

    private static bool IsExistingRosterConflictAllowed(
        Guid userId,
        RosterCandidateEligibilityDTO candidate,
        IReadOnlySet<Guid> existingRosterUserIds)
    {
        return existingRosterUserIds.Contains(userId) &&
               candidate.ReasonCodes.Count > 0 &&
               candidate.ReasonCodes.All(code => code == "duplicate_participation");
    }

    internal static bool IsExistingRegistrationEligibilityUsable(EligibilityResponseDTO eligibility) =>
        eligibility.Eligible || AreExistingRegistrationReasonsAllowed(eligibility.ReasonCodes);

    internal static bool IsExistingRosterEligibilityUsable(
        RosterCandidateEligibilityResponseDTO eligibility,
        IReadOnlySet<Guid> existingRosterUserIds)
    {
        if(eligibility.Eligible)
            return true;

        if(!AreExistingRegistrationReasonsAllowed(eligibility.ReasonCodes))
            return false;

        return eligibility.Candidates.All(candidate =>
            candidate.Eligible ||
            existingRosterUserIds.Contains(candidate.UserId) &&
            AreExistingRegistrationReasonsAllowed(candidate.ReasonCodes));
    }

    private static bool AreExistingRegistrationReasonsAllowed(IEnumerable<string> reasonCodes)
    {
        var reasons = reasonCodes.ToList();
        return reasons.Count > 0 && reasons.All(IsExistingRegistrationConflictCode);
    }

    private static bool IsExistingRegistrationConflictCode(string code) =>
        ExistingRegistrationConflictCodes.Contains(code);

    private string GetParticipantCountLabel() =>
        Localization.Get("Feature.tournaments.participantCount", _participants.Count);

    private string GetIndividualRegistrationStatusText(TournamentRegistrationStatus status) => status switch
    {
        TournamentRegistrationStatus.PendingConfirmation => Localization["Feature.tournaments.individualRegistrationPending"],
        TournamentRegistrationStatus.Active => Localization["Feature.tournaments.individualRegistrationActive"],
        _ => Localization.Get("Feature.tournaments.registrationStatus", GetRegistrationStatusLabel(status))
    };



    private string GetConfirmationLabel(RosterMemberConfirmationStatus status) => status switch
    {
        RosterMemberConfirmationStatus.AutoConfirmed => Localization["Feature.tournaments.confirmationCaptain"],
        RosterMemberConfirmationStatus.Pending => Localization["Feature.tournaments.confirmationPending"],
        RosterMemberConfirmationStatus.Confirmed => Localization["Feature.tournaments.confirmationConfirmed"],
        _ => Localization.Get("Feature.tournaments.confirmationStatus", status.ToString())
    };

    private string GetTeamRegistrationStateMessage()
    {
        if(!IsRegistrationOpen)
            return Localization["Feature.tournaments.registrationClosedNotScheduled"];

        if(!_isAuthenticated)
            return Localization["Feature.tournaments.signInCheckEligibility"];

        if(_registrationState?.PendingRosterConfirmation is not null)
            return Localization["Feature.tournaments.rosterInvitationSummary"];

        if(HasCaptainManagedRegistration)
            return Localization["Feature.tournaments.reviewCaptainRegistration"];

        return CurrentTeamRegistration?.Team is { } team
            ? Localization.Get("Feature.tournaments.registeredTeamManaged", team.Name)
            : Localization["Feature.tournaments.chooseTeamAndLineup"];
    }

    private string GetCurrentTeamRegistrationStatusText(TournamentRegistrationStatus status) => status switch
    {
        TournamentRegistrationStatus.PendingConfirmation => Localization["Feature.tournaments.teamRegistrationPending"],
        TournamentRegistrationStatus.Active => Localization["Feature.tournaments.teamRegistrationActive"],
        _ => Localization.Get("Feature.tournaments.registrationStatus", GetRegistrationStatusLabel(status))
    };

    private string GetUserLabel(PublicUserDTO? user)
    {
        if(user is null)
            return Localization["Feature.tournaments.unknownUser"];

        if(!string.IsNullOrWhiteSpace(user.Username))
            return user.Username.Trim();

        return Localization["Feature.tournaments.participant"];
    }

    private string GetReasonText(IEnumerable<string> reasonCodes)
    {
        var reasons = reasonCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(GetReasonLabel)
            .ToList();

        return reasons.Count == 0
            ? Localization["Feature.tournaments.notEligible"]
            : string.Join("; ", reasons);
    }

    private string GetReasonLabel(string reasonCode) => reasonCode.Trim().ToLowerInvariant() switch
    {
        "team_already_registered" => Localization["Feature.tournaments.teamAlreadyEntered"],
        "captain_duplicate_participation" => Localization["Feature.tournaments.captainAlreadyEntered"],
        "duplicate_participation" => Localization["Feature.tournaments.playerAlreadyEntered"],
        "roster_candidate_ineligible" => Localization["Feature.tournaments.rosterMemberIneligible"],
        _ => reasonCode.Replace('_', ' ').Trim()
    };

    private string GetLocalizedErrorMessage(Exception exception, string fallbackKey)
    {
        if(exception is ApiException apiException && !string.IsNullOrWhiteSpace(apiException.Content))
        {
            var content = apiException.Content!.Trim().Trim('"', '\'');
            if(content.Contains("duplicate_participation", StringComparison.OrdinalIgnoreCase))
                return Localization["Feature.tournaments.playerAlreadyEntered"];

            if(content.Contains("team_already_registered", StringComparison.OrdinalIgnoreCase))
                return Localization["Feature.tournaments.teamAlreadyEntered"];
        }

        return Localization[fallbackKey];
    }

    private string GetRegistrationKindLabel(TournamentRegistrationKind kind) => kind switch
    {
        TournamentRegistrationKind.Individual => Localization["Feature.tournaments.registrationKindIndividual"],
        TournamentRegistrationKind.Team => Localization["Feature.tournaments.registrationKindTeam"],
        _ => kind.ToString()
    };

    private string GetRegistrationStatusLabel(TournamentRegistrationStatus status) => status switch
    {
        TournamentRegistrationStatus.PendingConfirmation => Localization["Feature.tournaments.registrationStatusPending"],
        TournamentRegistrationStatus.Active => Localization["Feature.tournaments.registrationStatusActive"],
        _ => status.ToString()
    };

    private string GetRosterMemberAriaLabel(PublicUserDTO member, bool unavailable)
    {
        var label = GetUserLabel(member);
        return unavailable
            ? Localization.Get("Feature.tournaments.rosterUnavailableAria", label)
            : label;
    }

    private string GetTeamAriaLabel(string teamName, bool unavailable, bool selected)
    {
        var label = selected
            ? Localization.Get("Feature.tournaments.selectedTeamAria", teamName)
            : teamName;

        return unavailable
            ? Localization.Get("Feature.tournaments.teamUnavailableAria", label)
            : label;
    }


    private static bool IsUnauthorized(Exception exception) =>
        exception is UnauthorizedAccessException ||
        exception is ApiException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden };

    private static string GetErrorMessage(Exception exception, string fallback)
    {
        if(exception is ApiException apiException && !string.IsNullOrWhiteSpace(apiException.Content))
        {
            var content = apiException.Content!.Trim().Trim('"', '\'');
            if(content.Contains("duplicate_participation", StringComparison.OrdinalIgnoreCase))
                return "This player is already in another tournament entry.";

            if(content.Contains("team_already_registered", StringComparison.OrdinalIgnoreCase))
                return "This team already has a tournament entry.";

            return fallback;
        }

        return fallback;
    }

    internal sealed class TeamStateInvalidationGate
    {
        private readonly object _sync = new();
        private bool _isRunning;
        private bool _hasPendingRequest;

        public async Task RunAsync(Func<Task> action)
        {
            lock(_sync)
            {
                if(_isRunning)
                {
                    _hasPendingRequest = true;
                    return;
                }

                _isRunning = true;
            }

            try
            {
                while(true)
                {
                    await action();

                    lock(_sync)
                    {
                        if(!_hasPendingRequest)
                        {
                            _isRunning = false;
                            return;
                        }

                        _hasPendingRequest = false;
                    }
                }
            }
            catch
            {
                lock(_sync)
                {
                    _isRunning = false;
                    _hasPendingRequest = false;
                }

                throw;
            }
        }
    }

    private void ResetRegistrationContext()
    {
        _isLoadingRegistration = false;
        _isLoadingTeams = false;
        _isLoadingRoster = false;
        _isLoadingAdmin = false;
        _isSubmitting = false;
        _registrationState = null;
        _individualEligibility = null;
        _selectedTeamEligibility = null;
        _rosterEligibility = null;
        _teamSummary = null;
        _selectedTeamId = null;
        _pendingAdminRemovalRegistrationId = null;
        _selectedRosterUserIds.Clear();
        _teamEligibilityById.Clear();
        _rosterCandidatesById.Clear();
        _adminRegistrations.Clear();
        _registrationError = null;
        _registrationWarning = null;
        _adminError = null;
        _teamError = null;
        _isAuthenticated = false;
        _isAdmin = false;
        _activeTeamStep = 0;
        _hasDirtyRosterDraft = false;
        _isTeamUnregistrationConfirmationOpen = false;
        _teamUnregistrationConfirmationGeneration = null;
        _teamUnregistrationConfirmationTeamId = null;
        _isIndividualRegistrationConfirmationOpen = false;
        _isIndividualUnregistrationConfirmationOpen = false;
        _focusTeamUnregistrationConfirmation = false;
        _restoreTeamUnregistrationFocus = false;
    }

    private CancellationTokenSource BeginRegistrationLoad()
    {
        CancelRegistrationLoad();
        _registrationLoadInFlight = true;
        return _registrationLoadCancellation = new CancellationTokenSource();
    }

    private void EndRegistrationLoad(CancellationTokenSource cancellation)
    {
        if(ReferenceEquals(_registrationLoadCancellation, cancellation))
        {
            _registrationLoadCancellation = null;
            _registrationLoadInFlight = false;
            cancellation.Dispose();
        }
    }

    private void CancelRegistrationLoad()
    {
        var cancellation = _registrationLoadCancellation;
        _registrationLoadCancellation = null;
        _registrationLoadInFlight = false;
        if(cancellation is null)
            return;

        try
        {
            cancellation.Cancel();
        }
        catch(ObjectDisposedException)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async ValueTask DisposeRegistrationFocusTrapAsync()
    {
        var focusTrap = _registrationFocusTrap;
        _registrationFocusTrap = null;
        if(focusTrap is null)
            return;

        try
        {
            await focusTrap.InvokeVoidAsync("dispose");
            await focusTrap.DisposeAsync();
        }
        catch(JSDisconnectedException)
        {
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _requestGeneration++;
        CancelRegistrationLoad();
        TeamRealtimeService.TeamStateInvalidated -= HandleTeamStateInvalidatedAsync;
        NotificationService.RosterDecisionChanged -= HandleRosterDecisionChangedAsync;
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await DisposeRegistrationFocusTrapAsync();
    }
}
