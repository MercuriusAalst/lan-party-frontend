namespace Mercurius.LAN.Web.DTOs.Users;

public class PublicUserDTO
{
    public Guid Id { get; set; }
    public string? Username { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Firstname { get; set; }
    public string? Lastname { get; set; }
    public string? DiscordId { get; set; }
    public string? SteamId { get; set; }
    public string? RiotId { get; set; }
    public string? ProfilePictureUrl { get; set; }

    public static PublicUserDTO FromUser(UserDTO user)
    {
        return new PublicUserDTO
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                ? GetFallbackDisplayName(user.Firstname, user.Lastname, user.Username)
                : user.DisplayName,
            Firstname = user.Firstname,
            Lastname = user.Lastname,
            DiscordId = user.DiscordId,
            SteamId = user.SteamId,
            RiotId = user.RiotId,
            ProfilePictureUrl = user.ProfilePictureUrl
        };
    }

    public static PublicUserDTO FromUser(UserProfileDTO user)
    {
        return new PublicUserDTO
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                ? GetFallbackDisplayName(user.Firstname, user.Lastname, user.Username)
                : user.DisplayName,
            Firstname = user.Firstname,
            Lastname = user.Lastname,
            DiscordId = user.DiscordId,
            SteamId = user.SteamId,
            RiotId = user.RiotId,
            ProfilePictureUrl = user.ProfilePictureUrl
        };
    }

    private static string GetFallbackDisplayName(string? firstname, string? lastname, string? username)
    {
        var fullName = string.Join(" ", new[] { firstname, lastname }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));

        return string.IsNullOrWhiteSpace(fullName)
            ? username?.Trim() ?? string.Empty
            : fullName;
    }
}
