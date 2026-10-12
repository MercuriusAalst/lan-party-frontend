using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Participants.Teams;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Participants;
using Refit;
using System.Net;

namespace Mercurius.LAN.Web.Services;

public class TeamService : ITeamService
{
    private readonly ILANClient _lanClient;
    private readonly IConfiguration _configuration;

    public TeamService(ILANClient lanClient, IConfiguration configuration)
    {
        _lanClient = lanClient;
        _configuration = configuration;
    }

    public async Task<TeamPage> GetTeamsAsync(
        int page = 1,
        int pageSize = TeamPage.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePageArguments(page, pageSize);

        try
        {
            var requestPageSize = pageSize < TeamPage.MaximumPageSize
                ? pageSize + 1
                : pageSize;
            var teams = await _lanClient.GetTeamsAsync(page, requestPageSize, cancellationToken);
            var hasMore = teams.Count > pageSize;

            if(pageSize == TeamPage.MaximumPageSize && teams.Count == pageSize && page < int.MaxValue)
            {
                var nextPage = await _lanClient.GetTeamsAsync(page + 1, pageSize, cancellationToken);
                hasMore = nextPage.Count > 0;
            }

            var visibleTeams = teams.Take(pageSize).ToList();
            visibleTeams.ForEach(team => TeamAssetUrlResolver.Resolve(_configuration, team));
            return new TeamPage(visibleTeams, page, pageSize, hasMore);
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Load teams", exception);
        }
    }

    private static void ValidatePageArguments(int page, int pageSize)
    {
        if(page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");

        if(pageSize is < 1 or > TeamPage.MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size must be between 1 and {TeamPage.MaximumPageSize}.");
    }

    public async Task<PublicTeamProfileDTO?> GetPublicTeamByNameAsync(string teamName, CancellationToken cancellationToken = default)
    {
        var trimmedTeamName = teamName.Trim();
        if(string.IsNullOrWhiteSpace(trimmedTeamName))
            return null;

        try
        {
            var team = await _lanClient.GetPublicTeamByNameAsync(trimmedTeamName, cancellationToken);
            if(team is not null)
                TeamAssetUrlResolver.Resolve(_configuration, team);

            return team;
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Load public team profile", exception);
        }
    }

    public async Task<PublicProfileMatchSummariesDTO?> GetPublicTeamMatchSummariesAsync(string teamName, CancellationToken cancellationToken = default)
    {
        var trimmedTeamName = teamName.Trim();
        if(string.IsNullOrWhiteSpace(trimmedTeamName))
            return null;

        try
        {
            return await _lanClient.GetPublicTeamMatchSummariesAsync(trimmedTeamName, cancellationToken);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Load public team match summaries", exception);
        }
    }

    public async Task<CurrentUserTeamSummaryDTO> GetCurrentUserTeamSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _lanClient.GetCurrentUserTeamSummaryAsync(cancellationToken);
            TeamAssetUrlResolver.Resolve(_configuration, summary);
            return summary;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Load team summary", exception);
        }
    }

    public async Task<Team> CreateTeamAsync(CreateTeamDTO team)
    {
        try
        {
            var summary = await _lanClient.CreateTeamAsync(team);
            TeamAssetUrlResolver.Resolve(_configuration, summary);
            return new Team
            {
                Id = summary.Id,
                Name = summary.Name,
                CaptainUserId = summary.CaptainUserId,
                LogoUrl = summary.LogoUrl,
                Members = summary.Members
            };
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Create team", exception);
        }
    }

    public async Task<TeamInvite> InviteUserAsync(Guid teamId, Guid userId)
    {
        try
        {
            return await _lanClient.CreateTeamInviteAsync(teamId, new CreateTeamInviteRequestDTO { UserId = userId });
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Invite user to team", exception);
        }
    }

    public async Task<TeamInvite> CancelInviteAsync(Guid teamId, Guid inviteId)
    {
        try
        {
            return await _lanClient.CancelTeamInviteAsync(teamId, inviteId);
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Cancel team invite", exception);
        }
    }

    public async Task<TeamInvite> RespondToInviteAsync(Guid inviteId, bool accept)
    {
        try
        {
            return await _lanClient.RespondToCurrentUserTeamInviteAsync(inviteId, new RespondTeamInviteDTO { Accept = accept });
        }
        catch(ApiException exception)
        {
            var operation = accept ? "Accept team invite" : "Decline team invite";
            throw CreateServiceException(operation, exception);
        }
    }

    public async Task<TeamManagementSummaryDTO> LeaveTeamAsync(Guid teamId)
    {
        try
        {
            var team = await _lanClient.LeaveTeamAsync(teamId);
            TeamAssetUrlResolver.Resolve(_configuration, team);
            return team;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Leave team", exception);
        }
    }

    public async Task<TeamManagementSummaryDTO> RemoveMemberAsync(Guid teamId, Guid userId)
    {
        try
        {
            var team = await _lanClient.RemoveTeamMemberAsync(teamId, userId);
            TeamAssetUrlResolver.Resolve(_configuration, team);
            return team;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Remove team member", exception);
        }
    }

    public async Task<TeamManagementSummaryDTO> TransferCaptainAsync(Guid teamId, Guid newCaptainUserId)
    {
        try
        {
            var team = await _lanClient.TransferTeamCaptainAsync(teamId, new TransferCaptainDTO { NewCaptainUserId = newCaptainUserId });
            TeamAssetUrlResolver.Resolve(_configuration, team);
            return team;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Transfer team captain", exception);
        }
    }

    public async Task<TeamLogoResponseDTO> UploadLogoAsync(Guid teamId, Stream logoStream, string contentType, string fileName)
    {
        var logo = new StreamPart(logoStream, fileName, contentType);
        try
        {
            var response = await _lanClient.UploadTeamLogoAsync(teamId, logo);
            TeamAssetUrlResolver.Resolve(_configuration, response);
            return response;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Upload team logo", exception);
        }
    }

    public async Task<TeamLogoResponseDTO> RemoveLogoAsync(Guid teamId)
    {
        try
        {
            var response = await _lanClient.RemoveTeamLogoAsync(teamId);
            TeamAssetUrlResolver.Resolve(_configuration, response);
            return response;
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Remove team logo", exception);
        }
    }

    public async Task DeleteTeamAsync(Guid teamId)
    {
        try
        {
            await _lanClient.DeleteTeamAsync(teamId);
        }
        catch(ApiException exception)
        {
            throw CreateServiceException("Delete team", exception);
        }
    }

    private static TeamServiceException CreateServiceException(string operation, ApiException exception)
    {
        var message = GetUserFacingMessage(exception);
        var code = exception.GetApiError()?.Code;

        return new TeamServiceException(message, operation, exception.StatusCode, exception.Content, exception, code);
    }

    private static string GetUserFacingMessage(ApiException exception)
    {
        return exception.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "You need to sign in before managing teams.",
            HttpStatusCode.Forbidden => "You are not allowed to perform this team action.",
            HttpStatusCode.NotFound => "That team or invite could not be found.",
            _ => "The team action could not be completed right now."
        };
    }
}

public sealed class TeamServiceException : Exception
{
    public TeamServiceException(string message, string operation, HttpStatusCode statusCode, string? apiContent, Exception innerException, string? code = null) : base(message, innerException)
    {
        Operation = operation;
        StatusCode = statusCode;
        ApiContent = apiContent;
        Code = code;
    }

    public string Operation { get; }
    public HttpStatusCode StatusCode { get; }
    public string? ApiContent { get; }
    public string? Code { get; }
}
