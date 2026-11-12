namespace Mercurius.LAN.Web.Components.Shared;

/// <summary>
/// Builds links to the profile pages. <c>/users/{username}</c> is admin-only, so callers must only
/// render <see cref="User"/> links for administrators. Returns null when no route identifier exists.
/// </summary>
public static class ProfileRoutes
{
    public static string? User(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null : $"/users/{Uri.EscapeDataString(username.Trim())}";

    public static string? Team(string? teamName) =>
        string.IsNullOrWhiteSpace(teamName) ? null : $"/teams/{Uri.EscapeDataString(teamName.Trim())}";
}
