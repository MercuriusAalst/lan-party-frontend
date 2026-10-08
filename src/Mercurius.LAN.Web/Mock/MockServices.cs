using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Leaderboards;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.DTOs.Matches;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.DTOs.Registrations;
using Mercurius.LAN.Web.DTOs.Search;
using Mercurius.LAN.Web.DTOs.Sponsors;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Models.Tournaments;
using Mercurius.LAN.Web.Models.Matches;
using Mercurius.LAN.Web.Models.Participants;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using ModelTournamentStatus = Mercurius.LAN.Web.Models.Tournaments.TournamentStatus;

namespace Mercurius.LAN.Web.Mock;

internal sealed class MockTournamentService : ITournamentService
{
    private readonly MockBackendStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider? _authenticationStateProvider;

    public MockTournamentService(
        MockBackendStore store,
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider? authenticationStateProvider = null)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public Task<List<Tournament>> GetTournamentsAsync(
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetTournaments(page, pageSize));

    public Task<TournamentExtended?> GetTournamentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetTournament(id));

    public Task<TournamentExtended?> GetTournamentDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetTournament(id));

    public Task<List<PublicUserDTO>> GetAdminUsersAsync(
        string? query = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetAdminUsers(query, pageSize));

    public Task<TournamentExtended> CreateTournamentAsync(
        CreateTournamentDTO newTournament,
        string? tempFilePath,
        string? contentType,
        string? fileName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.CreateTournament(newTournament));

    public Task<TournamentExtended> UpdateTournamentAsync(
        Guid id,
        UpdateTournamentDTO updatedTournament,
        string? tempFilePath,
        string? contentType,
        string? fileName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.UpdateTournament(id, updatedTournament));

    public Task SetTournamentLifecycleStateAsync(
        Guid id,
        ModelTournamentStatus state,
        CancellationToken cancellationToken = default)
    {
        _store.SetTournamentLifecycleState(id, state);
        return Task.CompletedTask;
    }

    public Task DeleteTournamentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.DeleteTournament(id);
        return Task.CompletedTask;
    }

    public Task<TournamentExtended> ReplaceTournamentSponsorsAsync(
        Guid id,
        ReplaceTournamentSponsorsDTO sponsors,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.ReplaceTournamentSponsors(id, sponsors));

    public Task<Match> GetMatchByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetMatch(matchId));

    public async Task<MatchActionStateDTO> GetMatchActionStateAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.GetMatchActionState(persona, matchId);
    }

    public async Task<MatchOpponentProfileDTO> GetMatchOpponentProfileAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.GetMatchOpponentProfile(persona, matchId);
    }

    public async Task<Match> ConfirmMatchEndedAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.ConfirmMatchEnded(persona, matchId);
    }

    public async Task<Match> SubmitMatchScoreAsync(
        Guid matchId,
        SubmitMatchScoreDTO request,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.SubmitMatchScore(persona, matchId, request);
    }

    public async Task<Match> ForfeitMatchAsync(
        Guid matchId,
        ForfeitMatchDTO request,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.ForfeitMatch(persona, matchId, request);
    }

    public async Task<Match> ResolveMatchAsync(
        Guid matchId,
        ResolveMatchDTO request,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.ResolveMatch(persona, matchId, request);
    }

    public async Task<Match> ReverseMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.ReverseMatch(persona, matchId);
    }

    public async Task<Match> UpdateMatchScoresAsync(
        Guid matchId,
        UpdateMatchDTO updateMatchDTO,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.UpdateMatch(persona, matchId, updateMatchDTO);
    }

    public async Task<CurrentUserTournamentRegistrationStateDTO> GetCurrentUserTournamentRegistrationStateAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.GetCurrentUserTournamentRegistrationState(persona, tournamentId);
    }

    public async Task<EligibilityResponseDTO> CheckIndividualTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.CheckIndividualTournamentRegistrationEligibility(persona, tournamentId);
    }

    public async Task<EligibilityResponseDTO> CheckTeamTournamentRegistrationEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.CheckTeamTournamentRegistrationEligibility(persona, tournamentId, teamId);
    }

    public async Task<RosterCandidateEligibilityResponseDTO> CheckTeamRosterEligibilityAsync(
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.CheckTeamRosterEligibility(persona, tournamentId, teamId, roster);
    }

    public async Task<TournamentRegistrationDTO> RegisterCurrentUserForTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.RegisterCurrentUserForTournament(persona, tournamentId);
    }

    public async Task DeleteCurrentUserTournamentRegistrationAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        _store.DeleteCurrentUserTournamentRegistration(persona, tournamentId);
    }

    public async Task<TournamentRegistrationDTO> SubmitTeamTournamentRosterAsync(
        Guid tournamentId,
        Guid teamId,
        SubmitTeamRosterDTO roster,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.SubmitTeamTournamentRoster(persona, tournamentId, teamId, roster);
    }

    public async Task DeleteTeamTournamentRegistrationAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        _store.DeleteTeamTournamentRegistration(persona, tournamentId, teamId);
    }

    public async Task<TournamentRegistrationDTO> ConfirmTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.ConfirmTournamentRosterMember(persona, tournamentId, rosterMemberId);
    }

    public async Task DeclineTournamentRosterMemberAsync(
        Guid tournamentId,
        Guid rosterMemberId,
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        _store.DeclineTournamentRosterMember(persona, tournamentId, rosterMemberId);
    }

    public async Task<IReadOnlyList<PendingRosterConfirmationDTO>> GetPendingRosterConfirmationsAsync(
        CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.GetPendingRosterConfirmations(persona);
    }

    public Task<List<AdminTournamentRegistrationDTO>> GetAdminTournamentRegistrationsAsync(
        Guid tournamentId,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetAdminTournamentRegistrations(tournamentId, page, pageSize));

    public Task RemoveTournamentUserRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid userId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        _store.RemoveTournamentUserRegistrationAsAdmin(tournamentId, userId, reason);
        return Task.CompletedTask;
    }

    public Task RemoveTournamentTeamRegistrationAsAdminAsync(
        Guid tournamentId,
        Guid teamId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        _store.RemoveTournamentTeamRegistrationAsAdmin(tournamentId, teamId, reason);
        return Task.CompletedTask;
    }

    public Task<PublicLeaderboardDTO> GetLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetLeaderboard(tournamentId));

    public Task<AdminLeaderboardResponseDTO> GetAdminLeaderboardAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetAdminLeaderboard(tournamentId));

    public Task<AdminLeaderboardParticipantDTO> RecordLeaderboardAttemptAsync(
        Guid tournamentId,
        RecordLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.RecordLeaderboardAttempt(tournamentId, request));

    public Task<LeaderboardAttemptDTO> UpdateLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        UpdateLeaderboardAttemptDTO request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.UpdateLeaderboardAttempt(tournamentId, attemptId, request));

    public Task DeleteLeaderboardAttemptAsync(
        Guid tournamentId,
        Guid attemptId,
        Guid rowVersion,
        CancellationToken cancellationToken = default)
    {
        _store.DeleteLeaderboardAttempt(tournamentId, attemptId, rowVersion);
        return Task.CompletedTask;
    }

    private async Task<string> GetCurrentPersonaAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if(user?.Identity?.IsAuthenticated == true)
            return user.FindFirst("mock_persona")?.Value ?? "anonymous";

        if(_authenticationStateProvider is null)
            return "anonymous";

        var authenticationState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        user = authenticationState.User;
        if(user?.Identity?.IsAuthenticated != true)
            return "anonymous";

        return user.FindFirst("mock_persona")?.Value ?? "anonymous";
    }
}

internal sealed class MockTeamService : ITeamService
{
    private readonly MockBackendStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider? _authenticationStateProvider;

    public MockTeamService(
        MockBackendStore store,
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider? authenticationStateProvider = null)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public Task<TeamPage> GetTeamsAsync(
        int page = 1,
        int pageSize = TeamPage.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if(page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");

        if(pageSize is < 1 or > TeamPage.MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size must be between 1 and {TeamPage.MaximumPageSize}.");

        cancellationToken.ThrowIfCancellationRequested();

        var requestPageSize = pageSize < TeamPage.MaximumPageSize
            ? pageSize + 1
            : pageSize;
        var teams = _store.GetTeams(page, requestPageSize);
        var hasMore = teams.Count > pageSize;

        if(pageSize == TeamPage.MaximumPageSize && teams.Count == pageSize && page < int.MaxValue)
            hasMore = _store.GetTeams(page + 1, pageSize).Count > 0;

        return Task.FromResult(new TeamPage(teams.Take(pageSize).ToList(), page, pageSize, hasMore));
    }

    public Task<PublicTeamProfileDTO?> GetPublicTeamByNameAsync(string teamName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetPublicTeamByName(teamName));

    public Task<PublicProfileMatchSummariesDTO?> GetPublicTeamMatchSummariesAsync(string teamName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetPublicTeamMatchSummaries(teamName));

    public async Task<CurrentUserTeamSummaryDTO> GetCurrentUserTeamSummaryAsync(CancellationToken cancellationToken = default)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.GetCurrentUserTeamSummary(persona);
    }

    public async Task<Team> CreateTeamAsync(CreateTeamDTO team)
    {
        var persona = await GetCurrentPersonaAsync();
        var summary = _store.CreateCurrentUserTeam(persona, team);
        return new Team
        {
            Id = summary.Id,
            Name = summary.Name,
            CaptainUserId = summary.CaptainUserId,
            LogoUrl = summary.LogoUrl,
            Members = summary.Members
        };
    }

    public async Task<TeamInvite> InviteUserAsync(Guid teamId, Guid userId)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.CreateTeamInvite(persona, teamId, userId);
    }

    public async Task<TeamInvite> CancelInviteAsync(Guid teamId, Guid inviteId)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.CancelTeamInvite(persona, teamId, inviteId);
    }

    public async Task<TeamInvite> RespondToInviteAsync(Guid inviteId, bool accept)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.RespondToTeamInvite(persona, inviteId, accept);
    }

    public async Task<TeamManagementSummaryDTO> LeaveTeamAsync(Guid teamId)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.LeaveTeam(persona, teamId);
    }

    public async Task<TeamManagementSummaryDTO> RemoveMemberAsync(Guid teamId, Guid userId)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.RemoveTeamMember(persona, teamId, userId);
    }

    public async Task<TeamManagementSummaryDTO> TransferCaptainAsync(Guid teamId, Guid newCaptainUserId)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.TransferCaptain(persona, teamId, newCaptainUserId);
    }

    public async Task<TeamLogoResponseDTO> UploadLogoAsync(Guid teamId, Stream logoStream, string contentType, string fileName)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.UploadTeamLogo(persona, teamId, contentType, fileName);
    }

    public async Task<TeamLogoResponseDTO> RemoveLogoAsync(Guid teamId)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.RemoveTeamLogo(persona, teamId);
    }

    public async Task DeleteTeamAsync(Guid teamId)
    {
        var persona = await GetCurrentPersonaAsync();
        _store.DeleteCurrentUserTeam(persona, teamId);
    }

    private async Task<string> GetCurrentPersonaAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if(user?.Identity?.IsAuthenticated == true)
            return user.FindFirst("mock_persona")?.Value ?? "anonymous";

        if(_authenticationStateProvider is null)
            return "anonymous";

        var authenticationState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        user = authenticationState.User;
        if(user?.Identity?.IsAuthenticated != true)
            return "anonymous";

        return user.FindFirst("mock_persona")?.Value ?? "anonymous";
    }
}

internal sealed class MockGlobalSearchService : IGlobalSearchService
{
    private readonly MockBackendStore _store;

    public MockGlobalSearchService(MockBackendStore store)
    {
        _store = store;
    }

    public Task<SearchResponseDTO> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = _store.SearchGlobal(query)
            .Select(result => result.Type == GlobalSearchResultType.User
                ? new GlobalSearchResultDTO
                {
                    Type = result.Type,
                    DisplayLabel = result.DisplayLabel,
                    SupportingText = result.SupportingText,
                    Username = result.Username
                }
                : result)
            .ToList();
        return Task.FromResult(new SearchResponseDTO
        {
            Results = results,
            NextCursor = null,
            HasMore = false
        });
    }
}

internal sealed class MockUserSearchService : IUserSearchService
{
    private readonly MockBackendStore _store;

    public MockUserSearchService(MockBackendStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<GlobalSearchResultDTO>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GlobalSearchResultDTO> results = _store.SearchGlobal(query)
            .Where(result => result.Type == GlobalSearchResultType.User && result.UserId.HasValue)
            .Take(6)
            .ToList();

        return Task.FromResult(results);
    }
}

internal sealed class MockPublicProfileService : IPublicProfileService
{
    private readonly MockBackendStore _store;

    public MockPublicProfileService(MockBackendStore store)
    {
        _store = store;
    }

    public Task<PublicUserProfileDTO?> GetPublicUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.GetPublicUserByUsername(username));
    }

}

internal sealed class MockSponsorService : ISponsorService
{
    private readonly MockBackendStore _store;

    public MockSponsorService(MockBackendStore store)
    {
        _store = store;
    }

    public Task<IEnumerable<Sponsor>> GetSponsorsAsync() => Task.FromResult<IEnumerable<Sponsor>>(_store.GetSponsors());

    public Task<Sponsor> GetSponsorByIdAsync(int id) => Task.FromResult(_store.GetSponsorById(id));

    public Task<Sponsor> CreateSponsorAsync(SponsorManagementDTO createSponsorDTO, string? tempFilePath, string? contentType, string? fileName) =>
        Task.FromResult(_store.CreateSponsor(createSponsorDTO));

    public Task<Sponsor> UpdateSponsorAsync(int id, SponsorManagementDTO updateSponsorDTO, string? tempFilePath, string? contentType, string? fileName) =>
        Task.FromResult(_store.UpdateSponsor(id, updateSponsorDTO));

    public Task DeleteSponsorAsync(int id)
    {
        _store.DeleteSponsor(id);
        return Task.CompletedTask;
    }
}

internal sealed class MockUserClient : IUserClient
{
    private readonly MockBackendStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider? _authenticationStateProvider;

    public MockUserClient(
        MockBackendStore store,
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider? authenticationStateProvider = null)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<CurrentUserProfileResponse> GetCurrentUserProfileAsync()
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.GetCurrentProfile(persona);
    }

    public async Task<UserProfileDTO> CompleteCurrentUserProfileAsync(CompleteUserProfileRequest request)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.CompleteCurrentProfile(persona, request);
    }

    public async Task<UserProfileDTO> UpdateCurrentUserProfileAsync(UpdateUserProfileRequest request)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.UpdateCurrentProfile(persona, request);
    }

    public async Task<UsernameAvailabilityResponse> CheckUsernameAvailabilityAsync(string username)
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.CheckUsernameAvailability(persona, username);
    }

    public Task<UserActionResponse> ResendVerificationEmailAsync() =>
        Task.FromResult(new UserActionResponse("Mock verification email sent."));

    public Task<UserActionResponse> SendPasswordResetEmailAsync() =>
        Task.FromResult(new UserActionResponse("Mock password reset email sent."));

    public async Task<UserActionResponse> DeleteCurrentUserAsync()
    {
        var persona = await GetCurrentPersonaAsync();
        return _store.DeleteCurrentUser(persona);
    }

    public Task<IEnumerable<UserDTO>> GetAllUsersAsync() => Task.FromResult<IEnumerable<UserDTO>>(_store.GetUsers());

    public Task<UserDTO> GetUserByIdAsync(Guid id) => Task.FromResult(_store.GetUserById(id));

    public Task<UserDTO> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetUserByUsername(username));

    public Task<PublicProfileMatchSummariesDTO> GetUserMatchSummariesAsync(
        string username,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetPublicUserMatchSummaries(username)
            ?? throw new KeyNotFoundException("Mock user profile was not found."));

    public Task DeleteUserAsync(string username)
    {
        _store.DeleteUser(username);
        return Task.CompletedTask;
    }

    public Task<UserDTO> CreateUserAsync(CreateUserProfileRequest request)
    {
        throw new NotSupportedException("Mock admin user creation is not implemented.");
    }

    public Task<UserDTO> UpdateUserAsync(Guid id, UpdateUserProfileRequest request)
    {
        throw new NotSupportedException("Mock admin user updates are not implemented.");
    }

    public Task DeleteUserAsync(Guid id)
    {
        var user = _store.GetUserById(id);
        if(!string.IsNullOrWhiteSpace(user.Username))
            _store.DeleteUser(user.Username);

        return Task.CompletedTask;
    }

    public Task DeleteUserAccountAsync(string username)
    {
        _store.DeleteUser(username);
        return Task.CompletedTask;
    }

    private async Task<string> GetCurrentPersonaAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if(user?.Identity?.IsAuthenticated == true)
            return user.FindFirst("mock_persona")?.Value ?? "anonymous";

        if(_authenticationStateProvider is null)
            return "anonymous";

        var authenticationState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        user = authenticationState.User;
        if(user?.Identity?.IsAuthenticated != true)
            return "anonymous";

        return user.FindFirst("mock_persona")?.Value ?? "anonymous";
    }
}
