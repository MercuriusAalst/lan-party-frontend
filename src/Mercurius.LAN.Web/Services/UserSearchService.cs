using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Search;

namespace Mercurius.LAN.Web.Services;

public sealed class UserSearchService : IUserSearchService
{
    private readonly ILANClient _lanClient;

    public UserSearchService(ILANClient lanClient)
    {
        _lanClient = lanClient;
    }

    public async Task<IReadOnlyList<GlobalSearchResultDTO>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query.Trim();
        if(trimmedQuery.Length < 3)
            return [];

        var response = await _lanClient.SearchUsersAsync(trimmedQuery, pageSize: 6, cancellationToken: cancellationToken);
        return response.Results
            .Select(result => new GlobalSearchResultDTO
            {
                Type = GlobalSearchResultType.User,
                DisplayLabel = result.DisplayLabel,
                SupportingText = result.SupportingText ?? "User",
                UserId = result.Id,
                Username = result.Username,
                ProfilePictureUrl = result.ProfilePictureUrl
            })
            .ToList();
    }
}
