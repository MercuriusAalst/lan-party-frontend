using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Refit;
using System.Net;

namespace Mercurius.LAN.Web.Services;

public class PublicProfileService : IPublicProfileService
{
    private readonly ILANClient _lanClient;

    public PublicProfileService(ILANClient lanClient)
    {
        _lanClient = lanClient;
    }

    public async Task<PublicUserProfileDTO?> GetPublicUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var trimmedUsername = username.Trim();
        if(string.IsNullOrWhiteSpace(trimmedUsername))
            return null;

        try
        {
            return await _lanClient.GetPublicUserByUsernameAsync(trimmedUsername, cancellationToken);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

}
