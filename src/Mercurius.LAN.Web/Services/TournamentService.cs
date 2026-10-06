using System.Net.Http.Headers;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Matches;
using ModelTournamentStatus = Mercurius.LAN.Web.Models.Tournaments.TournamentStatus;

namespace Mercurius.LAN.Web.Services;

public sealed class TournamentService : ITournamentService
{
    private readonly ILANClient _lanClient;
    private readonly IConfiguration _configuration;

    public TournamentService(ILANClient lanClient, IConfiguration configuration)
    {
        _lanClient = lanClient;
        _configuration = configuration;
    }

    public Task<List<Tournament>> GetTournamentsAsync(
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetTournamentsAsync(page, pageSize, cancellationToken);

    public async Task<TournamentExtended?> GetTournamentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tournament = await _lanClient.GetTournamentByIdAsync(id, cancellationToken);
        return tournament is null ? null : ResolveTournament(tournament);
    }

    public Task<TournamentExtended?> GetTournamentDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetTournamentByIdAsync(id, cancellationToken);

    public async Task<TournamentExtended> CreateTournamentAsync(
        CreateTournamentDTO newTournament,
        string? tempFilePath,
        string? contentType,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        using var formData = BuildTournamentFormData(newTournament, tempFilePath, contentType, fileName);
        try
        {
            return ResolveTournament(await _lanClient.CreateTournamentAsync(formData, cancellationToken));
        }
        finally
        {
            DeleteTemporaryFile(tempFilePath);
        }
    }

    public async Task<TournamentExtended> UpdateTournamentAsync(
        Guid id,
        UpdateTournamentDTO updatedTournament,
        string? tempFilePath,
        string? contentType,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        using var formData = BuildTournamentFormData(updatedTournament, tempFilePath, contentType, fileName);
        try
        {
            return ResolveTournament(await _lanClient.UpdateTournamentAsync(id, formData, cancellationToken));
        }
        finally
        {
            DeleteTemporaryFile(tempFilePath);
        }
    }

    public async Task SetTournamentLifecycleStateAsync(
        Guid id,
        ModelTournamentStatus state,
        CancellationToken cancellationToken = default)
    {
        using var response = await _lanClient.SetTournamentLifecycleStateAsync(
            id,
            new UpdateTournamentLifecycleStateRequestDTO
            {
                State = state
            },
            cancellationToken);

        if(response.Error is not null)
            throw response.Error;
    }

    public Task DeleteTournamentAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _lanClient.DeleteTournamentAsync(id, cancellationToken);

    public async Task<TournamentExtended> ReplaceTournamentSponsorsAsync(
        Guid id,
        ReplaceTournamentSponsorsDTO sponsors,
        CancellationToken cancellationToken = default)
    {
        var tournament = await _lanClient.ReplaceTournamentSponsorsAsync(id, sponsors, cancellationToken);
        return ResolveTournament(tournament);
    }

    public Task<Match> GetMatchByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetMatchByIdAsync(matchId, cancellationToken);

    public Task<MatchActionStateDTO> GetMatchActionStateAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetMatchActionStateAsync(matchId, cancellationToken);

    public Task<MatchOpponentProfileDTO> GetMatchOpponentProfileAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetMatchOpponentProfileAsync(matchId, cancellationToken);

    public Task<Match> ConfirmMatchEndedAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        _lanClient.ConfirmMatchEndedAsync(matchId, cancellationToken);

    public Task<Match> SubmitMatchScoreAsync(
        Guid matchId,
        SubmitMatchScoreDTO request,
        CancellationToken cancellationToken = default) =>
        _lanClient.SubmitMatchScoreAsync(matchId, request, cancellationToken);

    public Task<Match> ForfeitMatchAsync(
        Guid matchId,
        ForfeitMatchDTO request,
        CancellationToken cancellationToken = default) =>
        _lanClient.ForfeitMatchAsync(matchId, request, cancellationToken);

    public Task<Match> ResolveMatchAsync(
        Guid matchId,
        ResolveMatchDTO request,
        CancellationToken cancellationToken = default) =>
        _lanClient.ResolveMatchAsync(matchId, request, cancellationToken);

    public Task<Match> ReverseMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        _lanClient.ReverseMatchAsync(matchId, cancellationToken);

    public Task<Match> UpdateMatchScoresAsync(
        Guid matchId,
        UpdateMatchDTO updateMatchDTO,
        CancellationToken cancellationToken = default) =>
        _lanClient.UpdateMatchAsync(matchId, updateMatchDTO, cancellationToken);

    public Task<CurrentUserTournamentRegistrationStateDTO> GetCurrentUserTournamentRegistrationStateAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetCurrentUserTournamentRegistrationStateAsync(tournamentId, cancellationToken);

    public Task<EligibilityResponseDTO> CheckIndividualTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        _lanClient.CheckIndividualTournamentRegistrationEligibilityAsync(tournamentId, cancellationToken);

    public Task<EligibilityResponseDTO> CheckTeamTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        _lanClient.CheckTeamTournamentRegistrationEligibilityAsync(tournamentId, teamId, cancellationToken);

    public Task<RosterCandidateEligibilityResponseDTO> CheckTeamRosterEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster,
        CancellationToken cancellationToken = default) =>
        _lanClient.CheckTeamRosterEligibilityAsync(tournamentId, teamId, roster, cancellationToken);

    public Task<TournamentRegistrationDTO> RegisterCurrentUserForTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        _lanClient.RegisterCurrentUserForTournamentAsync(tournamentId, cancellationToken);

    public Task DeleteCurrentUserTournamentRegistrationAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        _lanClient.DeleteCurrentUserTournamentRegistrationAsync(tournamentId, cancellationToken);

    public Task<TournamentRegistrationDTO> SubmitTeamTournamentRosterAsync(
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster,
        CancellationToken cancellationToken = default) =>
        _lanClient.SubmitTeamTournamentRosterAsync(tournamentId, teamId, roster, cancellationToken);

    public Task DeleteTeamTournamentRegistrationAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        _lanClient.DeleteTeamTournamentRegistrationAsync(tournamentId, teamId, cancellationToken);

    public Task<TournamentRegistrationDTO> ConfirmTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        CancellationToken cancellationToken = default) =>
        _lanClient.ConfirmTournamentRosterMemberAsync(
            tournamentId,
            rosterMemberId,
            new UpdateRosterMemberConfirmationRequestDTO
            {
                ConfirmationStatus = RosterMemberConfirmationStatus.Confirmed
            },
            cancellationToken);

    public Task DeclineTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        CancellationToken cancellationToken = default) =>
        _lanClient.DeclineTournamentRosterMemberAsync(tournamentId, rosterMemberId, cancellationToken);

    public async Task<IReadOnlyList<PendingRosterConfirmationDTO>> GetPendingRosterConfirmationsAsync(
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 50;
        var page = 1;
        var confirmations = new List<PendingRosterConfirmationDTO>();

        while(true)
        {
            var response = await _lanClient.GetPendingRosterConfirmationsAsync(page, pageSize, cancellationToken);
            confirmations.AddRange(response.Items);

            if(confirmations.Count >= response.TotalCount || response.Items.Count == 0)
                return confirmations;

            page++;
        }
    }

    public Task<List<AdminTournamentRegistrationDTO>> GetAdminTournamentRegistrationsAsync(
        Guid tournamentId,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetAdminTournamentRegistrationsAsync(tournamentId, page, pageSize, cancellationToken);

    public Task RemoveTournamentUserRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid userId,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        _lanClient.RemoveTournamentUserRegistrationAsAdminAsync(
            tournamentId,
            userId,
            new RemoveRegistrationDTO { Reason = reason },
            cancellationToken);

    public Task RemoveTournamentTeamRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid teamId,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        _lanClient.RemoveTournamentTeamRegistrationAsAdminAsync(
            tournamentId,
            teamId,
            new RemoveRegistrationDTO { Reason = reason },
            cancellationToken);

    public Task<PublicLeaderboardDTO> GetLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetLeaderboardAsync(tournamentId, cancellationToken);

    public Task<AdminLeaderboardResponseDTO> GetAdminLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        _lanClient.GetAdminLeaderboardAsync(tournamentId, cancellationToken);

    public Task<AdminLeaderboardParticipantDTO> RecordLeaderboardAttemptAsync(
        Guid tournamentId,
        RecordLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default) =>
        _lanClient.RecordLeaderboardAttemptAsync(tournamentId, request, cancellationToken);

    public Task<LeaderboardAttemptDTO> UpdateLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        UpdateLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default) =>
        _lanClient.UpdateLeaderboardAttemptAsync(tournamentId, attemptId, request, cancellationToken);

    public Task DeleteLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        Guid rowVersion,
        CancellationToken cancellationToken = default) =>
        _lanClient.DeleteLeaderboardAttemptAsync(tournamentId, attemptId, rowVersion, cancellationToken);

    private static MultipartFormDataContent BuildTournamentFormData(
        CreateTournamentDTO tournament,
        string? tempFilePath,
        string? contentType,
        string? fileName)
    {
        var formData = new MultipartFormDataContent
        {
            { new StringContent(tournament.Name ?? string.Empty), "Name" },
            { new StringContent(tournament.BracketType.ToString()), "BracketType" },
            { new StringContent(tournament.Format.ToString()), "Format" },
            { new StringContent(tournament.FinalsFormat.ToString()), "FinalsFormat" },
            { new StringContent(tournament.ParticipationMode?.ToString() ?? string.Empty), "ParticipationMode" },
            { new StringContent(tournament.PlannedStartTime.ToUtcIsoString()), "PlannedStartTime" },
            { new StringContent(tournament.AverageGameDurationMinutes.ToString()), "AverageGameDurationMinutes" },
            { new StringContent(tournament.RoundBreakDurationMinutes.ToString()), "RoundBreakDurationMinutes" }
        };

        AddTeamSize(formData, tournament.TeamSize);
        AddLeaderboardRankingMetric(formData, tournament.LeaderboardRankingMetric);
        AddImage(formData, tempFilePath, contentType, fileName);
        return formData;
    }

    private static MultipartFormDataContent BuildTournamentFormData(
        UpdateTournamentDTO tournament,
        string? tempFilePath,
        string? contentType,
        string? fileName)
    {
        var formData = new MultipartFormDataContent
        {
            { new StringContent(tournament.Name ?? string.Empty), "Name" },
            { new StringContent(tournament.BracketType.ToString()), "BracketType" },
            { new StringContent(tournament.Format.ToString()), "Format" },
            { new StringContent(tournament.FinalsFormat.ToString()), "FinalsFormat" },
            { new StringContent(tournament.ParticipationMode?.ToString() ?? string.Empty), "ParticipationMode" },
            { new StringContent(tournament.PlannedStartTime.ToUtcIsoString()), "PlannedStartTime" },
            { new StringContent(tournament.AverageGameDurationMinutes.ToString()), "AverageGameDurationMinutes" },
            { new StringContent(tournament.RoundBreakDurationMinutes.ToString()), "RoundBreakDurationMinutes" }
        };

        AddTeamSize(formData, tournament.TeamSize);
        AddLeaderboardRankingMetric(formData, tournament.LeaderboardRankingMetric);
        AddImage(formData, tempFilePath, contentType, fileName);
        return formData;
    }

    private static void AddLeaderboardRankingMetric(
        MultipartFormDataContent formData,
        LeaderboardRankingMetric? rankingMetric)
    {
        if(rankingMetric.HasValue)
            formData.Add(new StringContent(rankingMetric.Value.ToString()), "LeaderboardRankingMetric");
    }

    private static void AddTeamSize(MultipartFormDataContent formData, int? teamSize)
    {
        if(teamSize.HasValue)
            formData.Add(new StringContent(teamSize.Value.ToString()), "TeamSize");
    }

    private static void AddImage(
        MultipartFormDataContent formData,
        string? tempFilePath,
        string? contentType,
        string? fileName)
    {
        if(string.IsNullOrWhiteSpace(tempFilePath) || !File.Exists(tempFilePath))
            return;

        var streamContent = new StreamContent(File.OpenRead(tempFilePath));
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        formData.Add(streamContent, "Image", string.IsNullOrWhiteSpace(fileName) ? "tournament-image" : fileName);
    }

    private static void DeleteTemporaryFile(string? tempFilePath)
    {
        if(string.IsNullOrWhiteSpace(tempFilePath))
            return;

        try
        {
            if(File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
        catch(IOException)
        {
            // Cleanup is best effort; the API result should not be hidden by a temp-file failure.
        }
        catch(UnauthorizedAccessException)
        {
            // Cleanup is best effort; the API result should not be hidden by a temp-file failure.
        }
    }

    private TournamentExtended ResolveTournament(TournamentExtended tournament)
    {
        TournamentProjectionMapper.PopulateParticipantProjection(tournament);
        TeamAssetUrlResolver.Resolve(_configuration, tournament);
        return tournament;
    }
}
