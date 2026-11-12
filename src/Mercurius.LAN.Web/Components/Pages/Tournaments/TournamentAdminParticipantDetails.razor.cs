using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Mercurius.LAN.Web.Components.Pages.Tournaments;

/// <summary>
/// Owns the admin-only team roster dialog and player details dialog plus their lookups, so every
/// entry point (Participants section, match details) behaves the same. Only one dialog is open at a
/// time. The host decides who is an administrator through <see cref="Enabled"/>; the backend enforces
/// the admin role on both endpoints used here. The open methods are called from the host's event
/// handlers, after which the host re-renders and this component re-renders with it (its list
/// parameters always count as changed), so no explicit StateHasChanged is needed.
/// </summary>
public partial class TournamentAdminParticipantDetails
{
    /// <summary>Page size used when a team is not in <see cref="KnownRegistrations"/>.</summary>
    internal const int LookupPageSize = 100;
    private const int MaxLookupPages = 50;

    [Inject] private ITournamentService TournamentService { get; set; } = null!;
    [Inject] private IUserClient UserClient { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    [Parameter] public Guid TournamentId { get; set; }

    /// <summary>False closes and blocks everything (non-admin, popup-only render, admin data unavailable).</summary>
    [Parameter] public bool Enabled { get; set; }

    /// <summary>Teams that may be shown; a roster closes when its team is no longer in this list.</summary>
    [Parameter] public IReadOnlyList<ParticipantViewModel> Participants { get; set; } = [];

    /// <summary>Admin registrations the host already loaded: the fast path before any lookup.</summary>
    [Parameter] public IReadOnlyList<AdminTournamentRegistrationDTO>? KnownRegistrations { get; set; }

    private Guid? _teamId;
    private AdminTournamentRegistrationDTO? _lookedUpRegistration;
    private PublicUserDTO? _player;
    private Guid? _returnTeamId;
    private CancellationTokenSource? _lookup;
    private Guid _tournamentId;
    private bool _disposed;

    private ParticipantViewModel? RosterParticipant => Enabled && _teamId is { } teamId
        ? Participants.FirstOrDefault(participant => participant.Team?.Id == teamId)
        : null;

    private IReadOnlyList<TournamentRosterMemberDTO>? RosterMembers =>
        RosterParticipant?.Team is { } team && FindRegistration(team.Id) is { } registration
            ? (registration.RosterMembers ?? [])
                .Where(member => member.User is not null)
                .OrderByDescending(member => member.IsCaptain)
                .ThenBy(member => member.User.Username?.Trim() ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : null;

    private PublicUserDTO? Player => Enabled ? _player : null;

    protected override void OnParametersSet()
    {
        if(!Enabled || TournamentId != _tournamentId)
            Reset();

        _tournamentId = TournamentId;
    }

    /// <summary>Opens the roster of a team participant; looks the team up page by page only when it is not already known.</summary>
    public async Task OpenTeamAsync(ParticipantViewModel participant)
    {
        if(!Enabled || participant.Team is not { } team)
            return;

        CancelLookup();
        if(FindRegistration(team.Id) is null)
        {
            var lookup = StartLookup();
            try
            {
                var registration = await FindTeamRegistrationAsync(team.Id, lookup.Token);
                if(!IsCurrent(lookup))
                    return;

                if(registration is null)
                {
                    ToastService.ShowError(Localization["Feature.tournaments.teamMembersUnavailable"]);
                    return;
                }

                _lookedUpRegistration = registration;
            }
            catch(OperationCanceledException) when(lookup.IsCancellationRequested)
            {
                return;
            }
            catch(Exception)
            {
                if(IsCurrent(lookup))
                    ToastService.ShowError(Localization["Feature.tournaments.teamMembersUnavailable"]);
                return;
            }
            finally
            {
                EndLookup(lookup);
            }
        }

        ClosePlayer();
        _teamId = team.Id;
    }

    /// <summary>Opens player details without a way back to a roster (e.g. an individual participant).</summary>
    public Task OpenPlayerAsync(string username) => OpenPlayerDialogAsync(username, returnTeamId: null);

    private Task OpenPlayerFromRosterAsync(string username) => OpenPlayerDialogAsync(username, _teamId);

    /// <summary>
    /// Loads a player from the admin user endpoint (the data behind /users/{username}) and shows it in
    /// <see cref="UserInfoDialog"/>. The roster closes; <paramref name="returnTeamId"/> lets the back
    /// button reopen it. On failure nothing closes.
    /// </summary>
    private async Task OpenPlayerDialogAsync(string username, Guid? returnTeamId)
    {
        if(!Enabled || string.IsNullOrWhiteSpace(username))
            return;

        CancelLookup();
        var lookup = StartLookup();
        try
        {
            var user = await UserClient.GetUserByUsernameAsync(username.Trim(), lookup.Token);
            if(!IsCurrent(lookup))
                return;

            _player = PublicUserDTO.FromUser(user);
            _returnTeamId = returnTeamId;
            _teamId = null;
        }
        catch(OperationCanceledException) when(lookup.IsCancellationRequested)
        {
        }
        catch(Exception)
        {
            if(IsCurrent(lookup))
                ToastService.ShowError(Localization["Feature.tournaments.playerDetailsUnavailable"]);
        }
        finally
        {
            EndLookup(lookup);
        }
    }

    private async Task<AdminTournamentRegistrationDTO?> FindTeamRegistrationAsync(Guid teamId, CancellationToken cancellationToken)
    {
        for(var page = 1; page <= MaxLookupPages; page++)
        {
            var registrations = await TournamentService.GetAdminTournamentRegistrationsAsync(
                TournamentId, page, LookupPageSize, cancellationToken);
            if(FindActiveTeamRegistration(registrations, teamId) is { } registration)
                return registration;

            if(registrations.Count < LookupPageSize)
                break;
        }

        return null;
    }

    private AdminTournamentRegistrationDTO? FindRegistration(Guid teamId) =>
        FindActiveTeamRegistration(KnownRegistrations, teamId) ??
        (_lookedUpRegistration?.Team?.Id == teamId ? _lookedUpRegistration : null);

    private static AdminTournamentRegistrationDTO? FindActiveTeamRegistration(
        IEnumerable<AdminTournamentRegistrationDTO>? registrations,
        Guid teamId) =>
        registrations?.FirstOrDefault(candidate =>
            candidate.Kind == TournamentRegistrationKind.Team &&
            candidate.Status == TournamentRegistrationStatus.Active &&
            candidate.Team?.Id == teamId);

    private void CloseRoster()
    {
        // A lookup still in flight must not open its dialog after the admin closed the roster.
        CancelLookup();
        _teamId = null;
    }

    private void ClosePlayer()
    {
        _player = null;
        _returnTeamId = null;
    }

    private void ReturnToRoster()
    {
        var teamId = _returnTeamId;
        ClosePlayer();
        _teamId = teamId;
    }

    private void Reset()
    {
        CancelLookup();
        _teamId = null;
        _lookedUpRegistration = null;
        ClosePlayer();
    }

    private CancellationTokenSource StartLookup() => _lookup = new CancellationTokenSource();

    private bool IsCurrent(CancellationTokenSource lookup) =>
        !_disposed && !lookup.IsCancellationRequested && ReferenceEquals(_lookup, lookup);

    private void EndLookup(CancellationTokenSource lookup)
    {
        if(ReferenceEquals(_lookup, lookup))
            _lookup = null;
        lookup.Dispose();
    }

    private void CancelLookup()
    {
        _lookup?.Cancel();
        _lookup = null;
    }

    public void Dispose()
    {
        _disposed = true;
        CancelLookup();
    }
}
