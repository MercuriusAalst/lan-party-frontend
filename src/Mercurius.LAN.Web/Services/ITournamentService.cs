using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Matches;
using ModelTournamentStatus = Mercurius.LAN.Web.Models.Tournaments.TournamentStatus;

namespace Mercurius.LAN.Web.Services;

public interface ITournamentService
{
    Task<List<Tournament>> GetTournamentsAsync(
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);

    Task<TournamentExtended?> GetTournamentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TournamentExtended?> GetTournamentDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TournamentExtended> CreateTournamentAsync(
        CreateTournamentDTO newTournament,
        string? tempFilePath,
        string? contentType,
        string? fileName,
        CancellationToken cancellationToken = default);

    Task<TournamentExtended> UpdateTournamentAsync(
        Guid id,
        UpdateTournamentDTO updatedTournament,
        string? tempFilePath,
        string? contentType,
        string? fileName,
        CancellationToken cancellationToken = default);

    Task SetTournamentLifecycleStateAsync(
        Guid id,
        ModelTournamentStatus state,
        CancellationToken cancellationToken = default);

    Task DeleteTournamentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TournamentExtended> ReplaceTournamentSponsorsAsync(
        Guid id,
        ReplaceTournamentSponsorsDTO sponsors,
        CancellationToken cancellationToken = default);

    Task<Match> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<MatchActionStateDTO> GetMatchActionStateAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<MatchOpponentProfileDTO> GetMatchOpponentProfileAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<Match> ConfirmMatchEndedAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<Match> SubmitMatchScoreAsync(Guid matchId, SubmitMatchScoreDTO request, CancellationToken cancellationToken = default);

    Task<Match> ForfeitMatchAsync(Guid matchId, ForfeitMatchDTO request, CancellationToken cancellationToken = default);

    Task<Match> ResolveMatchAsync(Guid matchId, ResolveMatchDTO request, CancellationToken cancellationToken = default);

    Task<Match> ReverseMatchAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<Match> UpdateMatchScoresAsync(
        Guid matchId,
        UpdateMatchDTO updateMatchDTO,
        CancellationToken cancellationToken = default);

    Task<CurrentUserTournamentRegistrationStateDTO> GetCurrentUserTournamentRegistrationStateAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task<EligibilityResponseDTO> CheckIndividualTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task<EligibilityResponseDTO> CheckTeamTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<RosterCandidateEligibilityResponseDTO> CheckTeamRosterEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster,
        CancellationToken cancellationToken = default);

    Task<TournamentRegistrationDTO> RegisterCurrentUserForTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task DeleteCurrentUserTournamentRegistrationAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task<TournamentRegistrationDTO> SubmitTeamTournamentRosterAsync(
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster,
        CancellationToken cancellationToken = default);

    Task DeleteTeamTournamentRegistrationAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<TournamentRegistrationDTO> ConfirmTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        CancellationToken cancellationToken = default);

    Task DeclineTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingRosterConfirmationDTO>> GetPendingRosterConfirmationsAsync(
        CancellationToken cancellationToken = default);

    Task<List<AdminTournamentRegistrationDTO>> GetAdminTournamentRegistrationsAsync(
        Guid tournamentId,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);

    Task RemoveTournamentUserRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid userId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task RemoveTournamentTeamRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid teamId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<PublicLeaderboardDTO> GetLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task<AdminLeaderboardResponseDTO> GetAdminLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task<AdminLeaderboardParticipantDTO> RecordLeaderboardAttemptAsync(
        Guid tournamentId,
        RecordLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default);

    Task<LeaderboardAttemptDTO> UpdateLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        UpdateLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default);

    Task DeleteLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        Guid rowVersion,
        CancellationToken cancellationToken = default);
}
