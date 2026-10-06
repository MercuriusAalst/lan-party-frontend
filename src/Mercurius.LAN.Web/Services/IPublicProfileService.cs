using Mercurius.LAN.Web.DTOs.PublicProfiles;

namespace Mercurius.LAN.Web.Services;

public interface IPublicProfileService
{
    Task<PublicUserProfileDTO?> GetPublicUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
