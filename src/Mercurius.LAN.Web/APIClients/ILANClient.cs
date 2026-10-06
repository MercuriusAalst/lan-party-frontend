using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Search;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Models.Tournaments;
using Refit;

namespace Mercurius.LAN.Web.APIClients;

/// <summary>
/// Refit contract for the version 1 LAN API.
///
/// Route values intentionally include the API version and start with a leading slash. Refit
/// requires rooted route templates, while the registered HttpClient base address is normalized to
/// the host root so URI resolution preserves exactly one <c>/v1</c> segment.
/// </summary>
public interface ILANClient
{
    [Get("/v1/lan/tournaments")]
    Task<List<Tournament>> GetTournamentsAsync(
        [AliasAs("page")] int? page = null,
        [AliasAs("pageSize")] int? pageSize = null,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournaments/{tournamentId}")]
    Task<TournamentExtended?> GetTournamentByIdAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/search")]
    Task<SearchResponseDTO> SearchAsync(
        [AliasAs("query")] string query,
        [AliasAs("cursor")] string? cursor = null,
        [AliasAs("pageSize")] int? pageSize = null,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/users")]
    Task<UserSearchResponseDTO> SearchUsersAsync(
        [AliasAs("query")] string query,
        [AliasAs("cursor")] string? cursor = null,
        [AliasAs("pageSize")] int? pageSize = null,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/tournaments")]
    Task<TournamentExtended> CreateTournamentAsync(
        [Body] MultipartFormDataContent content,
        CancellationToken cancellationToken = default);

    [Patch("/v1/lan/tournaments/{tournamentId}")]
    Task<TournamentExtended> UpdateTournamentAsync(
        Guid tournamentId,
        [Body] MultipartFormDataContent formData,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/tournaments/{tournamentId}/sponsors")]
    Task<TournamentExtended> ReplaceTournamentSponsorsAsync(
        Guid tournamentId,
        [Body] ReplaceTournamentSponsorsDTO sponsors,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/tournaments/{tournamentId}")]
    Task DeleteTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/tournaments/{tournamentId}/lifecycle-state")]
    Task<IApiResponse> SetTournamentLifecycleStateAsync(
        Guid tournamentId,
        [Body] UpdateTournamentLifecycleStateRequestDTO request,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournaments/{tournamentId}/registrations/me")]
    Task<CurrentUserTournamentRegistrationStateDTO> GetCurrentUserTournamentRegistrationStateAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournaments/{tournamentId}/registrations/individual/eligibility")]
    Task<EligibilityResponseDTO> CheckIndividualTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournaments/{tournamentId}/registrations/teams/{teamId}/eligibility")]
    Task<EligibilityResponseDTO> CheckTeamTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/tournaments/{tournamentId}/registrations/teams/{teamId}/roster/eligibility")]
    Task<RosterCandidateEligibilityResponseDTO> CheckTeamRosterEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        [Body] SubmitTeamRosterDTO request,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/tournaments/{tournamentId}/registrations/individual/me")]
    Task<TournamentRegistrationDTO> RegisterCurrentUserForTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/tournaments/{tournamentId}/registrations/individual/me")]
    Task DeleteCurrentUserTournamentRegistrationAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/tournaments/{tournamentId}/registrations/teams/{teamId}/roster")]
    Task<TournamentRegistrationDTO> SubmitTeamTournamentRosterAsync(
        Guid tournamentId,
        Guid teamId,
        [Body] SubmitTeamRosterDTO request,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/tournaments/{tournamentId}/registrations/teams/{teamId}")]
    Task DeleteTeamTournamentRegistrationAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    [Patch("/v1/lan/tournaments/{tournamentId}/registrations/roster-members/{rosterMemberId}")]
    Task<TournamentRegistrationDTO> ConfirmTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        [Body] UpdateRosterMemberConfirmationRequestDTO request,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/tournaments/{tournamentId}/registrations/roster-members/{rosterMemberId}")]
    Task DeclineTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournament-roster-confirmations/me")]
    Task<PendingRosterConfirmationPageDTO> GetPendingRosterConfirmationsAsync(
        [AliasAs("page")] int page,
        [AliasAs("pageSize")] int pageSize,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournaments/{tournamentId}/registrations/admin")]
    Task<List<AdminTournamentRegistrationDTO>> GetAdminTournamentRegistrationsAsync(
        Guid tournamentId,
        [AliasAs("page")] int? page = null,
        [AliasAs("pageSize")] int? pageSize = null,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/tournaments/{tournamentId}/registrations/admin/users/{userId}")]
    Task RemoveTournamentUserRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid userId,
        [Body] RemoveRegistrationDTO request,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/tournaments/{tournamentId}/registrations/admin/teams/{teamId}")]
    Task RemoveTournamentTeamRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid teamId,
        [Body] RemoveRegistrationDTO request,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournaments/{tournamentId}/leaderboard")]
    Task<PublicLeaderboardDTO> GetLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/tournaments/{tournamentId}/leaderboard/attempts")]
    Task<AdminLeaderboardResponseDTO> GetAdminLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/tournaments/{tournamentId}/leaderboard/attempts")]
    Task<AdminLeaderboardParticipantDTO> RecordLeaderboardAttemptAsync(
        Guid tournamentId,
        [Body] RecordLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/tournaments/{tournamentId}/leaderboard/attempts/{attemptId}")]
    Task<LeaderboardAttemptDTO> UpdateLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        [Body] UpdateLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/tournaments/{tournamentId}/leaderboard/attempts/{attemptId}")]
    Task DeleteLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        [AliasAs("rowVersion")] Guid rowVersion,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/matches/{matchId}")]
    Task<Match> GetMatchByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/matches/{matchId}/me")]
    Task<MatchActionStateDTO> GetMatchActionStateAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/matches/{matchId}/opponent-profile")]
    Task<MatchOpponentProfileDTO> GetMatchOpponentProfileAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/matches/{matchId}/confirm-ended")]
    Task<Match> ConfirmMatchEndedAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/matches/{matchId}/score")]
    Task<Match> SubmitMatchScoreAsync(
        Guid matchId,
        [Body] SubmitMatchScoreDTO request,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/matches/{matchId}/forfeit")]
    Task<Match> ForfeitMatchAsync(
        Guid matchId,
        [Body] ForfeitMatchDTO request,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/matches/{matchId}/resolve")]
    Task<Match> ResolveMatchAsync(
        Guid matchId,
        [Body] ResolveMatchDTO request,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/matches/{matchId}/reverse")]
    Task<Match> ReverseMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/matches/{matchId}")]
    Task<Match> UpdateMatchAsync(
        Guid matchId,
        [Body] UpdateMatchDTO match,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/teams")]
    Task<List<Team>> GetTeamsAsync(
        [AliasAs("page")] int? page = null,
        [AliasAs("pageSize")] int? pageSize = null,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/teams/{teamId}")]
    Task<Team> GetTeamByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/teams")]
    Task<TeamManagementSummaryDTO> CreateTeamAsync(
        [Body] CreateTeamDTO team,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/teams/{teamId}")]
    Task DeleteTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/teams/me/summary")]
    Task<CurrentUserTeamSummaryDTO> GetCurrentUserTeamSummaryAsync(
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/teams/me/invites")]
    Task<IReadOnlyList<TeamInviteSummaryDTO>> GetCurrentUserTeamInvitesAsync(
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/teams/me/sent-invites")]
    Task<IReadOnlyList<TeamInviteSummaryDTO>> GetCurrentUserSentTeamInvitesAsync(
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/teams/{teamId}/members/me")]
    Task<TeamManagementSummaryDTO> LeaveTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/teams/{teamId}/members/{userId}")]
    Task<TeamManagementSummaryDTO> RemoveTeamMemberAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/teams/{teamId}/invites")]
    Task<TeamInvite> CreateTeamInviteAsync(
        Guid teamId,
        [Body] CreateTeamInviteRequestDTO request,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/teams/{teamId}/invites/{inviteId}")]
    Task<TeamInvite> CancelTeamInviteAsync(
        Guid teamId,
        Guid inviteId,
        CancellationToken cancellationToken = default);

    [Patch("/v1/lan/team-invites/{inviteId}")]
    Task<TeamInvite> RespondToCurrentUserTeamInviteAsync(
        Guid inviteId,
        [Body] RespondTeamInviteDTO response,
        CancellationToken cancellationToken = default);

    [Put("/v1/lan/teams/{teamId}/captain")]
    Task<TeamManagementSummaryDTO> TransferTeamCaptainAsync(
        Guid teamId,
        [Body] TransferCaptainDTO transfer,
        CancellationToken cancellationToken = default);

    [Multipart]
    [Put("/v1/lan/teams/{teamId}/logo")]
    Task<TeamLogoResponseDTO> UploadTeamLogoAsync(
        Guid teamId,
        [AliasAs("logo")] StreamPart logo,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/teams/{teamId}/logo")]
    Task<TeamLogoResponseDTO> RemoveTeamLogoAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/public/teams/{teamName}")]
    Task<PublicTeamProfileDTO> GetPublicTeamByNameAsync(
        string teamName,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/public/users/{username}")]
    Task<PublicUserProfileDTO> GetPublicUserByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/public/teams/{teamName}/match-summaries")]
    Task<PublicProfileMatchSummariesDTO> GetPublicTeamMatchSummariesAsync(
        string teamName,
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/sponsors")]
    Task<IEnumerable<Sponsor>> GetSponsorsAsync(
        CancellationToken cancellationToken = default);

    [Get("/v1/lan/sponsors/{sponsorId}")]
    Task<Sponsor> GetSponsorByIdAsync(
        int sponsorId,
        CancellationToken cancellationToken = default);

    [Post("/v1/lan/sponsors")]
    Task<Sponsor> CreateSponsorAsync(
        [Body] MultipartFormDataContent createSponsorFormData,
        CancellationToken cancellationToken = default);

    [Patch("/v1/lan/sponsors/{sponsorId}")]
    Task<Sponsor> UpdateSponsorAsync(
        int sponsorId,
        [Body] MultipartFormDataContent updateSponsorFormData,
        CancellationToken cancellationToken = default);

    [Delete("/v1/lan/sponsors/{sponsorId}")]
    Task DeleteSponsorAsync(
        int sponsorId,
        CancellationToken cancellationToken = default);
}
