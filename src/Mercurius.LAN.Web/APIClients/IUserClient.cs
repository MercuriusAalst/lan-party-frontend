using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.DTOs.PublicProfiles;
using Refit;

namespace Mercurius.LAN.Web.APIClients;

/// <summary>
/// Refit contract for the version 1 identity API. Rooted templates carry the single <c>/v1</c>
/// prefix while the configured base address remains at the API host root.
/// </summary>
public interface IUserClient
{
    [Get("/v1/lan/users/me")]
    Task<CurrentUserProfileResponse> GetCurrentUserProfileAsync();

    [Put("/v1/lan/users/me")]
    Task<UserProfileDTO> CompleteCurrentUserProfileAsync([Body] CompleteUserProfileRequest request);

    [Patch("/v1/lan/users/me")]
    Task<UserProfileDTO> UpdateCurrentUserProfileAsync([Body] UpdateUserProfileRequest request);

    [Get("/v1/lan/users/me/username-availability")]
    Task<UsernameAvailabilityResponse> CheckUsernameAvailabilityAsync([AliasAs("username")] string username);

    [Post("/v1/lan/users/me/resend-verification-email")]
    Task<UserActionResponse> ResendVerificationEmailAsync();

    [Post("/v1/lan/users/me/password-reset")]
    Task<UserActionResponse> SendPasswordResetEmailAsync();

    [Delete("/v1/lan/users/me")]
    Task<UserActionResponse> DeleteCurrentUserAsync();

    [Post("/v1/lan/users")]
    Task<UserDTO> CreateUserAsync([Body] CreateUserProfileRequest request);

    [Get("/v1/lan/users")]
    Task<IEnumerable<UserDTO>> GetAllUsersAsync();

    [Get("/v1/lan/users/{id}")]
    Task<UserDTO> GetUserByIdAsync(Guid id);

    [Get("/v1/lan/users/{username}")]
    Task<UserDTO> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);

    [Get("/v1/lan/users/{username}/match-summaries")]
    Task<PublicProfileMatchSummariesDTO> GetUserMatchSummariesAsync(
        string username,
        CancellationToken cancellationToken = default);

    [Patch("/v1/lan/users/{id}")]
    Task<UserDTO> UpdateUserAsync(Guid id, [Body] UpdateUserProfileRequest request);

    [Delete("/v1/lan/users/{id}")]
    Task DeleteUserAsync(Guid id);

    [Delete("/v1/lan/users/{username}")]
    Task DeleteUserAsync(string username);

    [Delete("/v1/lan/users/{username}/account")]
    Task DeleteUserAccountAsync(string username);
}
